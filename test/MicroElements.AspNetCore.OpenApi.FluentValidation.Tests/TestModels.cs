// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using FluentValidation;

// Marker class for WebApplicationFactory
public class TestMarker;

// ----- Models -----

public class TestCustomer
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? Phone { get; set; }
    public TestCustomerType Type { get; set; }
}

public enum TestCustomerType { Regular, Premium, Vip }

public class TestOrder
{
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Quantity { get; set; }
    public List<string> Items { get; set; } = new();
}

// ----- Validators -----

public class TestCustomerValidator : AbstractValidator<TestCustomer>
{
    public TestCustomerValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Age).GreaterThanOrEqualTo(0).LessThanOrEqualTo(150);
        RuleFor(x => x.Phone).Matches(@"^\+?[\d\s\-]+$").When(x => x.Phone != null);
    }
}

public class TestOrderValidator : AbstractValidator<TestOrder>
{
    public TestOrderValidator()
    {
        RuleFor(x => x.OrderNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(1_000_000);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 1000);
        RuleFor(x => x.Items).NotEmpty();
    }
}

// BigInteger model for Issue #146
public class TestBigIntegerModel
{
    public BigInteger Value { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class TestBigIntegerModelValidator : AbstractValidator<TestBigIntegerModel>
{
    public TestBigIntegerModelValidator()
    {
        RuleFor(x => x.Value).InclusiveBetween(new BigInteger(0), new BigInteger(999));
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
