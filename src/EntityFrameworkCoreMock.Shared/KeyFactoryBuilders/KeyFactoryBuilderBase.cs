/*
 * Copyright 2017-2021 Wouter Huysentruit
 *
 * See LICENSE file.
 */

using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace EntityFrameworkCoreMock.Shared.KeyFactoryBuilders;
public abstract class KeyFactoryBuilderBase : IKeyFactoryBuilder {


    public IKeyFactory<TEntity> BuildKeyFactory<TEntity>() {
        var keyProperties = ResolveKeyProperties<TEntity>();
        var keyFactory = BuildIdentityKeyFactory<TEntity>(keyProperties);
        keyFactory = keyFactory ?? BuildDefaultKeyFactory<TEntity, object>(keyProperties);
        return keyFactory;
    }

    public IKeyFactory<TEntity, TKey> BuildKeyFactory<TEntity, TKey>() where TKey : notnull {
        var keyProperties = ResolveKeyProperties<TEntity>();
        return BuildIdentityKeyFactory<TEntity, TKey>(keyProperties)
            ?? BuildDefaultKeyFactory<TEntity, TKey>(keyProperties);

    }

    // public Func<T, KeyContext, object> BuildKeyFactory<T>() {

    // }

    protected abstract PropertyInfo[] ResolveKeyProperties<T>();

    private static IKeyFactory<TEntity>? BuildIdentityKeyFactory<TEntity>(PropertyInfo[] keyProperties) {
        if (keyProperties.Length != 1) return null;
        var keyProperty = keyProperties[0];
        if (keyProperty == null) return null;
        var databaseGeneratedAttribute = keyProperty.GetCustomAttribute(typeof(DatabaseGeneratedAttribute)) as DatabaseGeneratedAttribute;
        if (databaseGeneratedAttribute?.DatabaseGeneratedOption != DatabaseGeneratedOption.Identity) return null;
        if (keyProperty.PropertyType == typeof(int)) {
            return BuildIdentityKeyFactory<TEntity, int>(keyProperties);
        } else if (keyProperty.PropertyType == typeof(long)) {
            return BuildIdentityKeyFactory<TEntity, long>(keyProperties);
        } else if (keyProperty.PropertyType == typeof(Guid)) {
            return BuildIdentityKeyFactory<TEntity, Guid>(keyProperties);
        }

        return null;
    }

    private static IKeyFactory<TEntity, TKey>? BuildIdentityKeyFactory<TEntity, TKey>(PropertyInfo[] keyProperties)
    where TKey : notnull {
        if (keyProperties.Length != 1) return null;
        var keyProperty = keyProperties[0];
        if (keyProperty == null) return null;
        var databaseGeneratedAttribute = keyProperty.GetCustomAttribute(typeof(DatabaseGeneratedAttribute)) as DatabaseGeneratedAttribute;
        if (databaseGeneratedAttribute?.DatabaseGeneratedOption != DatabaseGeneratedOption.Identity) return null;

        var entityArgument = Expression.Parameter(typeof(TEntity));
        var keyContextArgument = Expression.Parameter(typeof(KeyContext));

        return BuildIdentityKeyFactory<TEntity, TKey>(keyProperty, ctx => Expression.Property(ctx, nameof(KeyContext.NextIdentity)));

    }

    private static IKeyFactory<TEntity, TKey>? BuildIdentityKeyFactory<TEntity, TKey>(
        PropertyInfo keyProperty,
        Func<ParameterExpression, Expression> nextIdentity)
        where TKey : notnull {
        var entityArgument = Expression.Parameter(typeof(TEntity));
        var keyContextArgument = Expression.Parameter(typeof(KeyContext));
        var keyValueVariable = Expression.Variable(typeof(TKey));
        var generateBody = Expression.Block(typeof(TKey),
            new[] { keyValueVariable },
            Expression.Assign(keyValueVariable, Expression.Convert(Expression.Property(entityArgument, keyProperty), typeof(TKey))),
            Expression.IfThen(Expression.Equal(keyValueVariable, Expression.Default(typeof(TKey))),
                Expression.Block(
                    Expression.Assign(keyValueVariable, Expression.Convert(nextIdentity(keyContextArgument), typeof(TKey))),
                    Expression.Assign(Expression.Property(entityArgument, keyProperty), keyValueVariable)
                )
            ),
            Expression.Convert(keyValueVariable, typeof(TKey)));

        var getBody = Expression.Block(typeof(TKey),
            new[] { keyValueVariable },
            Expression.Property(entityArgument, keyProperty));

        var onGenerate = Expression.Lambda<Func<TEntity, KeyContext, TKey>>(generateBody, entityArgument, keyContextArgument).Compile();
        var onGet = Expression.Lambda<Func<TEntity, TKey>>(getBody, entityArgument).Compile();

        return new KeyFactory<TEntity, TKey>(onGet, onGenerate);
    }

    private static IKeyFactory<TEntity, TKey> BuildDefaultKeyFactory<TEntity, TKey>(PropertyInfo[] keyProperties)
    where TKey : notnull {
        var entityType = typeof(TEntity);

        var tupleType = Type.GetType($"System.Tuple`{keyProperties.Length}")
                        ?? throw new InvalidOperationException($"No tuple type found for {keyProperties.Length} generic arguments");
        var keyPropertyTypes = keyProperties.Select(x => x.PropertyType).ToArray();
        var concreteTupleType = tupleType.MakeGenericType(keyPropertyTypes);

        if (tupleType.IsAssignableTo(typeof(TKey))) {
            throw new ArgumentException($"{nameof(TKey)} must be assignable to {concreteTupleType.Name} (was {typeof(TKey).Name})", nameof(TKey));
        }

        var constructor = concreteTupleType.GetConstructor(keyPropertyTypes)
                        ?? throw new InvalidOperationException($"No tuple constructor found for key in {entityType.Name} entity");

        var entityArgument = Expression.Parameter(entityType);
        var keyContextArgument = Expression.Parameter(typeof(KeyContext));
        var newTupleExpression = Expression.New(constructor, keyProperties.Select(x => Expression.Property(entityArgument, x)));
        var onGenerate = Expression.Lambda<Func<TEntity, KeyContext, TKey>>(newTupleExpression, entityArgument, keyContextArgument).Compile();
        var onGet = Expression.Lambda<Func<TEntity, TKey>>(newTupleExpression, entityArgument).Compile();

        return new KeyFactory<TEntity, TKey>(onGet, onGenerate);
    }
}

public class KeyFactory<TEntity, TKey> : IKeyFactory<TEntity, TKey>
where TKey : notnull {
    private readonly Func<TEntity, TKey> onGetKey;
    private readonly Func<TEntity, KeyContext, TKey> onGetOrGenerate;

    public KeyFactory(Func<TEntity, TKey> getKey, Func<TEntity, KeyContext, TKey> getOrGenerateAndAssignKey) {
        this.onGetKey = getKey;
        this.onGetOrGenerate = getOrGenerateAndAssignKey;
    }
    public TKey GetKeyFromEntity(TEntity entity) {
        return this.onGetKey(entity);
    }

    public TKey GetOrGenerateAndAssingnKey(TEntity entity, KeyContext keyContext) {
        return this.onGetOrGenerate(entity, keyContext);
    }
}

