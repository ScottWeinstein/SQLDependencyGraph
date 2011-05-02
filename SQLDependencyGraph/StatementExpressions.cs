using System;

namespace ScottW
{
    public static class StatementExpressions
    {
        public static Func<T> Using<T, R>(Func<R> resFactory, Func<R, T> func) where R : IDisposable
        {
            return () =>
            {
                R r = default(R);
                try
                {
                    r = resFactory();
                    return func(r);
                }
                finally
                {
                    if (r != null)
                    {
                        r.Dispose();
                    }
                }
            };
        }

        public static TResult TryCatchThrow<TResult, TException>(Func<TResult> tryFunc, Action<TException> doCatchBlock) where TException : Exception
        {
            try
            {
                return tryFunc();
            }
            catch (TException ex)
            {
                if (ex is TException)
                {
                    doCatchBlock((TException)ex);
                }

                throw;
            }
        }

        public static TResult TryCatch<TResult, TException>(Func<TResult> tryFunc, Action<TException> doCatchBlock, Func<TResult> errorResult) where TException : Exception
        {
            try
            {
                return tryFunc();
            }
            catch (TException ex)
            {
                if (doCatchBlock != null)
                {
                    doCatchBlock((TException)ex);
                }

                return errorResult();
            }
        }
    }
}
