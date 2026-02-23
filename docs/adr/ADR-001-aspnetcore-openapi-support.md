# ADR-001: Поддержка Microsoft.AspNetCore.OpenApi (IOpenApiSchemaTransformer)

**Статус:** Принято
**Дата:** 2026-02-23
**Issue:** [#149](https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/149)
**Milestone:** v7.1.0

---

## 1. Контекст и проблема

С .NET 9 Microsoft предоставляет встроенную поддержку OpenAPI (`Microsoft.AspNetCore.OpenApi`):
- `builder.Services.AddOpenApi()` + `app.MapOpenApi()`
- Трансформеры: `IOpenApiSchemaTransformer`, `IOpenApiDocumentTransformer`, `IOpenApiOperationTransformer`

Пользователи мигрируют с Swashbuckle на встроенное решение. Наша библиотека должна поддерживать оба варианта.

**Ни .NET 9, ни .NET 10, ни будущие версии .NET НЕ включают маппинг FluentValidation на OpenAPI из коробки.** Microsoft предоставляет только инфраструктуру трансформеров, но не интеграцию с FluentValidation. Наша библиотека необходима для обоих версий.

### Референсная реализация (saithis)

Пользователь [saithis](https://github.com/saithis/dotnet-playground/tree/main/OpenApiFluentValidationApi) создал proof-of-concept:
- ~200 строк, standalone `FluentValidationSchemaTransformer : IOpenApiSchemaTransformer`
- Поддерживает: NotNull, NotEmpty, Length, MinLength, MaxLength, Between, Comparison, Regex, Email, CreditCard
- НЕ поддерживает: вложенные валидаторы (SetValidator), Include(), RuleForEach(), When/Unless, AllOf, кэширование, кастомизацию правил

### Различия .NET 9 vs .NET 10

| Аспект | .NET 9 | .NET 10 |
|--------|--------|---------|
| `IOpenApiSchemaTransformer` | Есть | Есть |
| `GetOrCreateSchemaAsync()` | Нет | Есть |
| `context.Document` | Нет | Есть |
| Microsoft.OpenApi версия | v1.x | v2.x (ломающий API) |
| `OPENAPI_V2` нужен | Нет | Да |

Наша библиотека нужна для обоих версий. Различия только в API модели `OpenApiSchema`.

---

## 2. Рассмотренные варианты

### Вариант A: Новый отдельный пакет (ВЫБРАН)

```
MicroElements.OpenApi.FluentValidation          (ядро, generic абстракции)
    ^                      ^
    |                      |
Swashbuckle пакет    НОВЫЙ: AspNetCore.OpenApi пакет
(ISchemaFilter)      (IOpenApiSchemaTransformer)
```

- `MicroElements.AspNetCore.OpenApi.FluentValidation`
- Targets: `net9.0;net10.0`
- Зависимости: ядро + `Microsoft.AspNetCore.OpenApi` (БЕЗ Swashbuckle)
- Дублирует ~630 строк OpenApiSchema-специфичного кода
- Планируется извлечение в Фазе 2 (v7.2)

### Вариант B: Извлечение общего OpenApi-слоя (отложен на v7.2)

```
MicroElements.OpenApi.FluentValidation           (ядро, generic)
    ^
MicroElements.OpenApi.FluentValidation.Rules      (НОВЫЙ: общие OpenApiSchema правила)
    ^                      ^
Swashbuckle пакет     НОВЫЙ: AspNetCore.OpenApi пакет
```

- Извлекает общий код в shared пакет
- `[TypeForwardedTo]` для совместимости
- Ноль дублирования, но сложнее и риск breaking changes

### Вариант C: Минимальная интеграция (отклонён)

- Только net9.0, без OPENAPI_V2
- Максимум дублирования, нет net10.0

---

## 3. Решение: Вариант A (Поэтапный)

**Фаза 1 (v7.1.0):** Новый пакет с контролируемым дублированием
**Фаза 2 (v7.2):** Извлечение общего слоя, очистка неймспейсов

### Обоснование
- Быстрый выпуск без breaking changes для существующих пользователей
- Дублирование управляемо (~630 строк, определённый набор файлов)
- Следует прецеденту NSwag пакета
- Извлечение общего слоя запланировано на v7.2

---

## 4. Архитектура нового пакета

### 4.1 Граф зависимостей

```
MicroElements.AspNetCore.OpenApi.FluentValidation
  -> MicroElements.OpenApi.FluentValidation (ядро)
       -> FluentValidation >= 12.0.0
       -> Microsoft.Extensions.Logging.Abstractions
       -> Microsoft.Extensions.Options
  -> Microsoft.AspNetCore.OpenApi (>= 9.0.0 для net9.0, >= 10.0.0 для net10.0)
  [НЕТ зависимости от Swashbuckle]
```

### 4.2 Структура файлов

```
src/MicroElements.AspNetCore.OpenApi.FluentValidation/
│
├── MicroElements.AspNetCore.OpenApi.FluentValidation.csproj
├── GlobalUsings.cs
│
├── FluentValidationSchemaTransformer.cs       # НОВЫЙ: IOpenApiSchemaTransformer
├── AspNetCoreSchemaGenerationContext.cs        # НОВЫЙ: ISchemaGenerationContext<OpenApiSchema>
├── AspNetCoreSchemaProvider.cs                 # НОВЫЙ: ISchemaProvider<OpenApiSchema>
│
├── FluentValidationRule.cs                     # КОПИЯ из Swashbuckle
├── DefaultFluentValidationRuleProvider.cs      # КОПИЯ из Swashbuckle
├── OpenApiRuleContext.cs                       # КОПИЯ из Swashbuckle
│
├── OpenApi/
│   ├── OpenApiSchemaCompatibility.cs           # КОПИЯ из Swashbuckle
│   └── OpenApiExtensions.cs                    # КОПИЯ из Swashbuckle
│
├── Generation/
│   └── SystemTextJsonNameResolver.cs           # КОПИЯ из Swashbuckle
│
└── AspNetCore/
    ├── AspNetJsonSerializerOptions.cs          # КОПИЯ из Swashbuckle
    ├── ReflectionDependencyInjectionExtensions.cs # КОПИЯ из Swashbuckle
    ├── ServiceCollectionExtensions.cs          # НОВЫЙ: DI регистрация
    └── OpenApiOptionsExtensions.cs             # НОВЫЙ: AddFluentValidationRules()
```

### 4.3 Классификация файлов

| Файл | Тип | Источник |
|------|-----|----------|
| `.csproj` | Новый | - |
| `GlobalUsings.cs` | Копия | Swashbuckle GlobalUsings.cs |
| `FluentValidationSchemaTransformer.cs` | **Новый** | По паттерну FluentValidationRules.cs |
| `AspNetCoreSchemaGenerationContext.cs` | **Новый** | По паттерну SchemaGenerationContext.cs |
| `AspNetCoreSchemaProvider.cs` | **Новый** | net9: stub, net10: GetOrCreateSchemaAsync |
| `FluentValidationRule.cs` | Копия | Swashbuckle FluentValidationRule.cs |
| `DefaultFluentValidationRuleProvider.cs` | Копия | Swashbuckle DefaultFluentValidationRuleProvider.cs |
| `OpenApiRuleContext.cs` | Копия | Swashbuckle OpenApiRuleContext.cs |
| `OpenApiSchemaCompatibility.cs` | Копия | Swashbuckle OpenApiSchemaCompatibility.cs |
| `OpenApiExtensions.cs` | Копия | Swashbuckle OpenApiExtensions.cs |
| `SystemTextJsonNameResolver.cs` | Копия | Swashbuckle SystemTextJsonNameResolver.cs |
| `AspNetJsonSerializerOptions.cs` | Копия | Swashbuckle AspNetJsonSerializerOptions.cs |
| `ReflectionDependencyInjectionExtensions.cs` | Копия | Swashbuckle ReflectionDependencyInjectionExtensions.cs |
| `ServiceCollectionExtensions.cs` | **Новый** | По паттерну Swashbuckle ServiceCollectionExtensions.cs |
| `OpenApiOptionsExtensions.cs` | **Новый** | - |

---

## 5. User-Facing API

### Регистрация в Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register FluentValidation validators
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Register FluentValidation OpenAPI support
builder.Services.AddFluentValidationRulesToOpenApi();

// Add OpenApi with the FluentValidation transformer
builder.Services.AddOpenApi(options =>
{
    options.AddFluentValidationRules();
});

var app = builder.Build();
app.MapOpenApi();
app.Run();
```

### Миграция со Swashbuckle

```diff
// NuGet
- MicroElements.Swashbuckle.FluentValidation
+ MicroElements.AspNetCore.OpenApi.FluentValidation

// Program.cs
- services.AddSwaggerGen();
- services.AddFluentValidationRulesToSwagger();
+ services.AddFluentValidationRulesToOpenApi();
+ services.AddOpenApi(options => options.AddFluentValidationRules());

// Namespace
- using MicroElements.Swashbuckle.FluentValidation.AspNetCore;
+ using MicroElements.AspNetCore.OpenApi.FluentValidation;
```

---

## 6. Известные ограничения

1. **Вложенные валидаторы на .NET 9**: `SetValidator<T>()` sub-schema resolution ограничен (нет `GetOrCreateSchemaAsync`). Полная поддержка на .NET 10.
2. **Гранулярность трансформера**: `IOpenApiSchemaTransformer` вызывается per-schema (включая property schemas). Нужно фильтровать по `context.JsonPropertyInfo == null`.
3. **Дублирование кода**: ~630 строк дублированы из Swashbuckle пакета. Баг-фиксы нужно применять в обоих местах до v7.2 (Фаза 2).

---

## 7. Верификация

### 7.1 Сборка
```bash
dotnet build MicroElements.Swashbuckle.FluentValidation.sln
```
- Все проекты компилируются без ошибок

### 7.2 Тесты
```bash
dotnet test MicroElements.Swashbuckle.FluentValidation.sln
```
- Существующие тесты проходят (нет регрессий)
- Новые тесты для всех типов правил проходят

### 7.3 Sample приложение
```bash
cd samples/SampleAspNetCoreOpenApi
dotnet run
# Открыть /openapi/v1.json
```
- OpenAPI документ содержит validation constraints

### 7.4 Зависимости
- НЕТ транзитивной зависимости от Swashbuckle
- Есть зависимость на MicroElements.OpenApi.FluentValidation и Microsoft.AspNetCore.OpenApi
