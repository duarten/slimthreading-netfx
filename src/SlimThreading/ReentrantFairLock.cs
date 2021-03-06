﻿// Copyright 2011 Carlos Martins, Duarte Nunes
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//  

using System;
using System.Threading;

#pragma warning disable 0420

namespace SlimThreading {

    //
    // This class implements a reentrant fair lock (same as the .NET Mutex).
    //

    public sealed class StReentrantFairLock : StWaitable, IMonitorLock {
        private readonly StFairLock flock;

        private const int UNOWNED = 0;
        private int owner;
        private int count;

        public StReentrantFairLock(int spinCount) {
            flock = new StFairLock(spinCount);
        }

        public StReentrantFairLock() {
            flock = new StFairLock();
        }

        internal override bool _AllowsAcquire {
            get { return flock._AllowsAcquire || 
                         owner == Thread.CurrentThread.ManagedThreadId; 
            }
        }

        //
        // Exits the lock.
        //

        public void Exit() {
            if (Thread.CurrentThread.ManagedThreadId != owner) {
                throw new StSynchronizationLockException();
            }

            if (count != 0) {
                count--;
                return;
            }

            owner = UNOWNED;
            flock.Exit();
        }

        internal override bool _TryAcquire() {
            int tid = Thread.CurrentThread.ManagedThreadId;
            
            if (flock._TryAcquire()) {
                owner = tid;
                return true;
            }

            if (owner == tid) {
                count++;
                return true;
            }
            
            return false;
        }

        internal override bool _Release() {
            if (owner != Thread.CurrentThread.ManagedThreadId) {
                return false;
            }
            Exit();
            return true;
        }

        internal override WaitBlock _WaitAnyPrologue(StParker pk, int key,
                                                     ref WaitBlock hint, ref int sc) {
            return _TryAcquire() ? null : flock._WaitAnyPrologue(pk, key, ref hint, ref sc);
        }

        internal override WaitBlock _WaitAllPrologue(StParker pk, ref WaitBlock hint,
                                                     ref int sc) {
            return _AllowsAcquire ? null : flock._WaitAllPrologue(pk, ref hint, ref sc);
        }

        internal override void _WaitEpilogue() {
            owner = Thread.CurrentThread.ManagedThreadId;
        }

        internal override void _UndoAcquire() {
            Exit();
        }

        internal override void _CancelAcquire(WaitBlock wb, WaitBlock hint) {
            flock._CancelAcquire(wb, hint);
        }

        internal override Exception _SignalException {
            get { return new StSynchronizationLockException(); }
        }

        #region IMonitorLock

        bool IMonitorLock.IsOwned {
            get { return (owner == Thread.CurrentThread.ManagedThreadId); }
        }

        int IMonitorLock.ExitCompletely() {

            //
            // Set the lock's acquisition count to zero (i.e., one pending acquire),
            // release it and return the previous state.
            //

            int pc = count;
            count = 0;
            Exit();
            return pc;
        }

        void IMonitorLock.Reenter(int waitStatus, int pvCount) {

            //
            // If the wait on the condition variable failed, we must do a full acquire.
            // Either way, we must set the "owner" field to the current thread.
            //

            if (waitStatus != StParkStatus.Success) {
                flock.WaitOne();
            }

            owner = Thread.CurrentThread.ManagedThreadId;

            //
            // Restore the previous state of the lock.
            //

            count = pvCount;
        }

        //
        // Enqueues the specified wait block in the lock's queue as a locked 
        // acquire request. When this method is callead the lock is owned by
        // the current thread.
        //

        void IMonitorLock.EnqueueWaiter(WaitBlock wb) {
            flock.EnqueueLockedWaiter(wb);
        }

        #endregion
    }
}
