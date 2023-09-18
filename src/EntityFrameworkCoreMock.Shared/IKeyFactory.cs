/*
 * Copyright 2017-2021 Wouter Huysentruit
 *
 * See LICENSE file.
 */

namespace EntityFrameworkCoreMock;
public interface IKeyFactory<T> {
    object GetOrGenerateAndAssingnKey(T entity, KeyContext keyContext);
    object GetKeyFromEntity(T entity);
}

public interface IKeyFactory<TEntity, TKey> : IKeyFactory<TEntity>
where TKey : notnull {
    new TKey GetOrGenerateAndAssingnKey(TEntity entity, KeyContext keyContext);
    new TKey GetKeyFromEntity(TEntity entity);
    object IKeyFactory<TEntity>.GetOrGenerateAndAssingnKey(TEntity entity, KeyContext keyContext) => GetOrGenerateAndAssingnKey(entity, keyContext);
    object IKeyFactory<TEntity>.GetKeyFromEntity(TEntity entity) => GetKeyFromEntity(entity);

}
