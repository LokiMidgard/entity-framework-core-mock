/*
 * Copyright 2017-2021 Wouter Huysentruit
 *
 * See LICENSE file.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace EntityFrameworkCoreMock {
    public class DbContextMock<TDbContext> : Mock<TDbContext>
        where TDbContext : DbContext {
        private readonly IKeyFactoryBuilder _keyFactoryBuilder;
        private readonly Dictionary<Type, IDbSetMock> _dbSetCache = new Dictionary<Type, IDbSetMock>();

        public DbContextMock(params object[] args)
            : this(new CompositeKeyFactoryBuilder(), args) {
        }

        private DbContextMock(IKeyFactoryBuilder keyFactoryBuilder, params object[] args)
            : base(args) {
            _keyFactoryBuilder = keyFactoryBuilder ?? throw new ArgumentNullException(nameof(keyFactoryBuilder));
            Reset();
        }

        public DbSetMock<TEntity, TKey> CreateDbSetMock<TEntity, TKey>(Expression<Func<TDbContext, DbSet<TEntity>>> dbSetSelector, IEnumerable<TEntity>? initialEntities = null, Func<TEntity, TEntity>? handleAddedEntity = null)
            where TKey : notnull
            where TEntity : class
            => CreateDbSetMock(dbSetSelector, _keyFactoryBuilder.BuildKeyFactory<TEntity, TKey>(), initialEntities, handleAddedEntity);

        public DbSetMock<TEntity, TKey> CreateDbSetMock<TEntity, TKey>(Expression<Func<TDbContext, DbSet<TEntity>>> dbSetSelector, IKeyFactory<TEntity, TKey> entityKeyFactory, IEnumerable<TEntity>? initialEntities = null, Func<TEntity, TEntity>? handleAddedEntity = null)
            where TKey : notnull
            where TEntity : class {
            if (dbSetSelector == null) throw new ArgumentNullException(nameof(dbSetSelector));
            if (entityKeyFactory == null) throw new ArgumentNullException(nameof(entityKeyFactory));

            var entityType = typeof(TEntity);
            if (_dbSetCache.ContainsKey(entityType)) throw new ArgumentException($"DbSetMock for entity {entityType.Name} already created", nameof(dbSetSelector));
            var mock = new DbSetMock<TEntity, TKey>(initialEntities, entityKeyFactory, handleAddedEntity: handleAddedEntity);
            Setup(dbSetSelector).Returns(() => mock.Object);
            Setup(x => x.Set<TEntity>()).Returns(() => mock.Object);
            Setup(x => x.Add(It.IsAny<TEntity>())).Returns<TEntity>(entity => mock.Object.Add(entity));
            Setup(x => x.AddAsync(It.IsAny<TEntity>(), It.IsAny<CancellationToken>())).Returns<TEntity, CancellationToken>((entity, token) => mock.Object.AddAsync(entity, token));
            Setup(x => x.Update(It.IsAny<TEntity>())).Returns<TEntity>(entity => mock.Object.Update(entity));
            Setup(x => x.Remove(It.IsAny<TEntity>())).Returns<TEntity>(entity => mock.Object.Remove(entity));
            _dbSetCache.Add(entityType, mock);
            return mock;
        }

        public void Reset() {
            MockExtensions.Reset(this);
            _dbSetCache.Clear();
            Setup(x => x.SaveChanges()).Returns(SaveChanges);
            Setup(x => x.SaveChangesAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(SaveChanges);
            Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SaveChanges);

            Setup(x => x.AddRangeAsync(It.IsAny<object[]>())).Returns<object[]>((entitys) => {
                this.Object.AddRangeAsync(entitys, default);
                return Task.CompletedTask;
            });
            Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<object>>(), It.IsAny<CancellationToken>())).Returns<IEnumerable<object>, CancellationToken>((entitys, token) => {
                this.Object.AddRange(entitys);
                return Task.CompletedTask;
            });
            Setup(x => x.AddRange(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).Callback<object[]>((entitys) => {
                this.Object.AddRange((IEnumerable<object>)entitys);
            });
            Setup(x => x.AddRange(It.IsAny<IEnumerable<object>>())).Callback<IEnumerable<object>>((entitys) => {
                foreach (var entity in entitys) {
                    var currentType = entity.GetType();
                    IDbSetMock? cachedSet = null;
                    while (currentType is not null && !_dbSetCache.TryGetValue(currentType, out cachedSet)) {
                        currentType = currentType.BaseType;
                    }
                    if (cachedSet is null) {
                        throw new InvalidOperationException($"Did not find entity set for {entity}");
                    }
                    cachedSet.Add(entity);
                }
            });



            var lazyMockDbFacade = new Lazy<Mock<DatabaseFacade>>(() => {
                var mockDbFacade = new Mock<DatabaseFacade>(Object);
                var mockTransaction = new Mock<IDbContextTransaction>();
                mockDbFacade.Setup(x => x.BeginTransaction()).Returns(mockTransaction.Object);
                mockDbFacade.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mockTransaction.Object);
                return mockDbFacade;
            });

            Setup(x => x.Database).Returns(() => lazyMockDbFacade.Value.Object);
        }

        // Facilitates unit-testing
        internal void RegisterDbSetMock<TEntity>(Expression<Func<TDbContext, DbSet<TEntity>>> dbSetSelector, IDbSetMock dbSet)
            where TEntity : class {
            var entityType = typeof(TEntity);
            _dbSetCache.Add(entityType, dbSet);
        }

        private int SaveChanges() => _dbSetCache.Values.Aggregate(0, (seed, dbSet) => seed + dbSet.SaveChanges());
    }
}
