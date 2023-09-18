/*
 * Copyright 2017-2021 Wouter Huysentruit
 *
 * See LICENSE file.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EntityFrameworkCoreMock {
    public sealed class CompositeKeyFactoryBuilder : IKeyFactoryBuilder {
        private readonly AttributeBasedKeyFactoryBuilder<KeyAttribute> _attributeBasedKeyFactoryBuilder = new();
        private readonly ConventionBasedKeyFactoryBuilder _conventionBasedKeyFactoryBuilder = new();


        public IKeyFactory<TEntity, TKey> BuildKeyFactory<TEntity, TKey>() where TKey : notnull {
            var exceptions = new List<Exception>();

            try {
                return _attributeBasedKeyFactoryBuilder.BuildKeyFactory<TEntity, TKey>();
            } catch (Exception ex) {
                exceptions.Add(ex);
            }

            try {
                return _conventionBasedKeyFactoryBuilder.BuildKeyFactory<TEntity, TKey>();
            } catch (Exception ex) {
                exceptions.Add(ex);
            }

            throw new AggregateException($"No key factory could be created for entity type {typeof(TEntity).Name}, see inner exceptions", exceptions);
        }

        public IKeyFactory<TEntity> BuildKeyFactory<TEntity>() {
            var exceptions = new List<Exception>();

            try {
                return _attributeBasedKeyFactoryBuilder.BuildKeyFactory<TEntity>();
            } catch (Exception ex) {
                exceptions.Add(ex);
            }

            try {
                return _conventionBasedKeyFactoryBuilder.BuildKeyFactory<TEntity>();
            } catch (Exception ex) {
                exceptions.Add(ex);
            }

            throw new AggregateException($"No key factory could be created for entity type {typeof(TEntity).Name}, see inner exceptions", exceptions);
        }
    }
}
