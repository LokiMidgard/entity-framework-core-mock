/*
 * Copyright 2017-2021 Wouter Huysentruit
 *
 * See LICENSE file.
 */

using System;

namespace EntityFrameworkCoreMock {
    public sealed class KeyContext {
        private long _nextIdentity = 1;

        public long NextIdentity => _nextIdentity++;
        public void EnsureIdUsed(long id) => _nextIdentity = Math.Max(_nextIdentity, id + 1);
    }
}
