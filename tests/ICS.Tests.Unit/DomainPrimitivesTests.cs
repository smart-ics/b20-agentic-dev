namespace ICS.Tests.Unit;

using System;
using System.Collections.Generic;
using FluentAssertions;
using ICS.Core.Domain;
using ICS.Core.Time;
using Xunit;

/// <summary>
/// Unit tests verifying core domain building blocks (Entity, ValueObject, DomainEvents, SystemClock).
/// Architecture §19.8 and §19.11.
/// </summary>
public class DomainPrimitivesTests
{
    private sealed record TestDomainEvent(string Name) : DomainEvent;

    private sealed class TestEntity : Entity<Guid>
    {
        public TestEntity(Guid id)
        {
            Id = id;
        }
    }

    private sealed class TestValueObject : ValueObject
    {
        public string Code { get; }
        public int Value { get; }

        public TestValueObject(string code, int value)
        {
            Code = code;
            Value = value;
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Code;
            yield return Value;
        }
    }

    [Fact]
    public void Entity_WithSameId_ShouldBeEqual()
    {
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        var entity2 = new TestEntity(id);

        entity1.Should().Be(entity2);
        (entity1 == entity2).Should().BeTrue();
        (entity1 != entity2).Should().BeFalse();
        entity1.GetHashCode().Should().Be(entity2.GetHashCode());
    }

    [Fact]
    public void Entity_WithDifferentId_ShouldNotBeEqual()
    {
        var entity1 = new TestEntity(Guid.NewGuid());
        var entity2 = new TestEntity(Guid.NewGuid());

        entity1.Should().NotBe(entity2);
        (entity1 == entity2).Should().BeFalse();
        (entity1 != entity2).Should().BeTrue();
    }

    [Fact]
    public void Entity_DomainEventsCollection_ShouldRecordAndClearEvents()
    {
        var entity = new TestEntity(Guid.NewGuid());
        var ev = new TestDomainEvent("Created");

        entity.AddDomainEvent(ev);
        entity.DomainEvents.Should().ContainSingle().Which.Should().Be(ev);

        entity.ClearDomainEvents();
        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ValueObject_WithEqualComponents_ShouldBeEqual()
    {
        var vo1 = new TestValueObject("USD", 100);
        var vo2 = new TestValueObject("USD", 100);

        vo1.Should().Be(vo2);
        (vo1 == vo2).Should().BeTrue();
        vo1.GetHashCode().Should().Be(vo2.GetHashCode());
    }

    [Fact]
    public void ValueObject_WithDifferentComponents_ShouldNotBeEqual()
    {
        var vo1 = new TestValueObject("USD", 100);
        var vo2 = new TestValueObject("EUR", 100);

        vo1.Should().NotBe(vo2);
        (vo1 == vo2).Should().BeFalse();
    }

    [Fact]
    public void SystemClock_UtcNow_ShouldReturnCurrentUtcTime()
    {
        ISystemClock clock = new SystemClock();
        var before = DateTime.UtcNow;
        var now = clock.UtcNow;
        var after = DateTime.UtcNow;

        now.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
