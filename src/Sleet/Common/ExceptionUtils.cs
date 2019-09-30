// Shared source file

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NuGet.Common;

namespace Sleet
{
    public static class ExceptionUtils
    {
        /// <summary>
        /// [Type] Exception message
        /// [Type] Exception message
        /// </summary>
        internal static void LogException(Exception ex, ILogger logger)
        {
            LogException(ex, logger, logLevel: LogLevel.Error, showType: true, message: null);
        }

        /// <summary>
        /// [Type] Exception message
        /// [Type] Exception message
        /// </summary>
        internal static void LogException(Exception ex, ILogger logger, LogLevel logLevel)
        {
            LogException(ex, logger, logLevel, showType: true, message: null);
        }

        /// <summary>
        /// [Type] Exception message
        /// [Type] Exception message
        /// </summary>
        internal static void LogException(Exception ex, ILogger logger, LogLevel logLevel, bool showType)
        {
            LogException(ex, logger, logLevel, showType, message: null);
        }

        /// <summary>
        /// Message
        ///   [Type] Exception message
        ///   [Type] Exception message
        /// </summary>
        internal static void LogException(Exception ex, ILogger logger, LogLevel logLevel, bool showType, string message)
        {
            if (ex == null)
            {
                throw new ArgumentNullException(nameof(ex));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            // Log for debugging
            foreach (var innerEx in GetExceptions(ex, includeInner: true))
            {
                logger.Log(LogLevel.Debug, innerEx.ToString());
            }

            // Log
            logger.Log(logLevel, GetExceptionMessage(ex, showType, message));
        }

        /// <summary>
        /// [Type] Exception message
        /// [Type] Exception message
        /// </summary>
        internal static string GetExceptionMessage(Exception ex)
        {
            return GetExceptionMessage(ex, showType: true, message: null);
        }

        /// <summary>
        /// [Type] Exception message
        /// [Type] Exception message
        /// </summary>
        internal static string GetExceptionMessage(Exception ex, bool showType)
        {
            return GetExceptionMessage(ex, showType, message: null);
        }

        /// <summary>
        /// Message
        ///   - [Type] Exception message
        ///   - [Type] Exception message
        /// </summary>
        /// <remarks>Displays exceptions top level if no message is given.</remarks>
        internal static string GetExceptionMessage(Exception ex, bool showType, string message)
        {
            if (ex == null)
            {
                throw new ArgumentNullException(nameof(ex));
            }

            var sb = new StringBuilder();
            var hasMessage = !string.IsNullOrEmpty(message);

            var exceptions = GetExceptions(ex, includeInner: false).ToList();

            if (hasMessage)
            {
                sb.AppendLine(message);
            }

            foreach (var exception in exceptions)
            {
                var exMessage = showType ? FormatExceptionWithName(exception) : exception.Message;

                if (hasMessage)
                {
                    sb.AppendLine("\t- " + exMessage);
                }
                else
                {
                    sb.AppendLine(exMessage);
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Return the root exception thrown.
        /// </summary>
        internal static Exception Unwrap(Exception ex)
        {
            return GetExceptions(ex, includeInner: false).FirstOrDefault();
        }

        /// <summary>
        /// [Type] Message
        /// </summary>
        internal static string FormatExceptionWithName(Exception ex)
        {
            if (ex == null)
            {
                throw new ArgumentNullException(nameof(ex));
            }

            return $"[{ex.GetType()}] {ex.Message}";
        }

        /// <summary>
        /// Flatten AggregateExceptions
        /// </summary>
        internal static IEnumerable<Exception> GetExceptions(Exception ex, bool includeInner)
        {
            if (ex != null)
            {
                if (ex is AggregateException ag)
                {
                    return ag.InnerExceptions.SelectMany(e => GetExceptions(e, includeInner));
                }
                else if (ex is TargetInvocationException te)
                {
                    return GetExceptions(te.InnerException, includeInner);
                }
                else
                {
                    var exceptions = new List<Exception>() { ex };
                    if (includeInner)
                    {
                        exceptions.AddRange(GetExceptions(ex.InnerException, includeInner));
                    }
                    return exceptions;
                }
            }

            return Enumerable.Empty<Exception>();
        }
    }
}
