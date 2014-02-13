// ***********************************************************************
// Assembly         : TextQL
// Author           : Joan Caron
// Created          : 02-13-2014
// License			: MIT License (MIT) http://opensource.org/licenses/MIT
// Last Modified By : Joan Caron
// Last Modified On : 02-13-2014
// ***********************************************************************
// <copyright file="StringExtensions.cs" company="Joan Caron">
//     Copyright (c) Joan Caron. All rights reserved.
// </copyright>
// <summary>
//      A .Net Version of the tiny but great textql tool 
//      https://github.com/dinedal/textql 
// </summary>
// ***********************************************************************
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace TextQL
{
    /// <summary>
    /// Class StringExtensions.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Execute Replace function safely.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="oldValue">The old value.</param>
        /// <param name="newValue">The new value.</param>
        /// <returns>System.String.</returns>
        public static string SafeReplace(this string value, string oldValue, string newValue)
        {
            return !string.IsNullOrEmpty(value) ? value.Replace(oldValue,newValue) : string.Empty;
        }

        /// <summary>
        /// Cleans the string value so it will be "Path-friendly".
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>System.String.</returns>
        public static string ToCleanPath(this string value)
        {
            var invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            var regex = new Regex(string.Format("[{0}]", Regex.Escape(invalidChars)),RegexOptions.Compiled);
            value = regex.Replace(value, "");
            return Path.GetFullPath(value);
        }
    }
}