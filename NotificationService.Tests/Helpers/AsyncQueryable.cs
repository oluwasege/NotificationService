using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Query;

namespace NotificationService.Tests.Helpers;

public static class MockAsyncQueryable
{
    public static IQueryable<T> Build<T>(List<T> data)
    {
        return new TestAsyncEnumerable<T>(data);
    }
}

internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    internal TestAsyncQueryProvider(IQueryProvider inner)
    {
        _inner = inner;
    }

    public IQueryable CreateQuery(Expression expression)
    {
        return new TestAsyncEnumerable<TEntity>(expression);
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new TestAsyncEnumerable<TElement>(expression);
    }

    public object? Execute(Expression expression)
    {
        return _inner.Execute(expression);
    }

    public TResult Execute<TResult>(Expression expression)
    {
        return _inner.Execute<TResult>(expression);
    }

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        var resultType = typeof(TResult).GetGenericArguments()[0];
        var executeMethod = typeof(IQueryProvider)
            .GetMethod(
                name: nameof(IQueryProvider.Execute),
                genericParameterCount: 1,
                types: new[] { typeof(Expression) });

        if (executeMethod is null)
        {
            throw new InvalidOperationException("Could not find generic Execute method on IQueryProvider.");
        }

        var executionResult = executeMethod
            .MakeGenericMethod(resultType)
            .Invoke(_inner, [expression]);

        var fromResultMethod = typeof(Task).GetMethod(nameof(Task.FromResult), BindingFlags.Static | BindingFlags.Public);
        if (fromResultMethod is null)
        {
            throw new InvalidOperationException("Could not find Task.FromResult method.");
        }

        var task = fromResultMethod.MakeGenericMethod(resultType).Invoke(null, new[] { executionResult });
        if (task is null)
        {
            throw new InvalidOperationException("Task.FromResult returned null.");
        }

        return (TResult)task;
    }
}

internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable)
        : base(enumerable)
    { }

    public TestAsyncEnumerable(Expression expression)
        : base(expression)
    { }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }

    IQueryProvider IQueryable.Provider
    {
        get { return new TestAsyncQueryProvider<T>(this); }
    }
}

internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    public TestAsyncEnumerator(IEnumerator<T> inner)
    {
        _inner = inner;
    }

    public ValueTask DisposeAsync()
    {
        _inner.Dispose();
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> MoveNextAsync()
    {
        return ValueTask.FromResult(_inner.MoveNext());
    }

    public T Current
    {
        get { return _inner.Current; }
    }
}
