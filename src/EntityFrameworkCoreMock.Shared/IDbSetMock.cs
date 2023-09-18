/*
 * Copyright 2017-2021 Wouter Huysentruit
 *
 * See LICENSE file.
 */

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EntityFrameworkCoreMock {
    public interface IDbSetMock {


        public EntityEntry Remove(object entity);
        void Add(object entity);
        void Update(object entity);
        int SaveChanges();
    }
}
