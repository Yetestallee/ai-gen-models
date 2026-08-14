using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MeshyWorkspace.Tests
{
    public static class TestAssert
    {
        public static async Task<TException> ThrowsAsync<TException>(Func<Task> action) where TException : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (TException e)
            {
                return e;
            }

            Assert.Fail("Expected exception " + typeof(TException).Name + " was not thrown.");
            return null;
        }
    }
}
