# Issue #241 package test

Run from the repository root with the SDK in `global.json` and the .NET 10 runtime. The probe installs the same `net10.0` adapter package with two Swashbuckle versions. It checks the actual Swagger response for JSON, multipart, a generic-only request content dictionary, a URL-encoded form, and an operation without a body. The Swashbuckle 11 run also checks schema enrichment for a media type reference and verifies that its encoding stays intact.

```bash
set -e
version=7.3.1-issue241.local
scratch="$(mktemp -d)"
feed="$scratch/feed"
export NUGET_PACKAGES="$scratch/cache"

for id in MicroElements.OpenApi.FluentValidation MicroElements.Swashbuckle.FluentValidation; do
  dotnet pack "src/$id/$id.csproj" -c Release --source https://api.nuget.org/v3/index.json \
    --artifacts-path "$scratch/pack" -p:TargetFrameworks=net10.0 \
    -p:GeneratePackageOnBuild=false -p:PackageVersion="$version" -o "$feed"
done

probe=test/Issue241.PackageProbe/Issue241.PackageProbe.csproj
for swash in 10.2.1 11.0.1; do
  out="$scratch/$swash"
  dotnet restore "$probe" -p:AdapterVersion="$version" -p:SwashbuckleVersion="$swash" \
    --artifacts-path "$out" --source "$feed" --source https://api.nuget.org/v3/index.json
  dotnet build "$probe" -c Release --no-restore -p:AdapterVersion="$version" \
    -p:SwashbuckleVersion="$swash" --artifacts-path "$out"
  for mode in 0 1; do
    ISSUE241_DOCUMENT_FILTER="$mode" dotnet "$out/bin/Issue241.PackageProbe/release/Issue241.PackageProbe.dll" \
      "$out/swagger-$mode.json" > "$out/probe-$mode.log" 2>&1
    if grep -Fq MissingMethodException "$out/probe-$mode.log"; then
      cat "$out/probe-$mode.log" >&2
      exit 1
    fi
  done
done

dll=MicroElements.Swashbuckle.FluentValidation.dll
unzip -p "$feed/MicroElements.Swashbuckle.FluentValidation.$version.nupkg" "lib/net10.0/$dll" > "$scratch/$dll"
for swash in 10.2.1 11.0.1; do
  cmp "$scratch/$dll" "$scratch/$swash/bin/Issue241.PackageProbe/release/$dll"
done
printf 'Verified package bytes and probe results in %s\n' "$scratch"
```

Mode `0` runs the default operation filter. Mode `1` runs the opt-in document filter. Results and logs are written to the scratch directory.
