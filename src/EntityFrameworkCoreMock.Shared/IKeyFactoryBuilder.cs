/*
 * Copyright 2017-2021 Wouter Huysentruit
 *
 * See LICENSE file.
 */

using System;

namespace EntityFrameworkCoreMock; 
public interface IKeyFactoryBuilder {
    IKeyFactory<TEntity> BuildKeyFactory<TEntity>();
    IKeyFactory<TEntity, TKey> BuildKeyFactory<TEntity, TKey>()
        where TKey : notnull;
}
