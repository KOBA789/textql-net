#region License
// ***********************************************************************
// Assembly         : TextQL
// Author           : Joan Caron
// Created          : 02-11-2014
// License			: MIT License (MIT) http://opensource.org/licenses/MIT
// Last Modified By : Joan Caron
// Last Modified On : 02-13-2014
// ***********************************************************************
// <copyright file="Program.cs" company="Joan Caron">
//     Copyright (c) Joan Caron. All rights reserved.
// </copyright>
// <summary>
//      A .Net Version of the tiny but great textql tool 
//      https://github.com/dinedal/textql 
// </summary>
// ***********************************************************************
#endregion

#region Usings

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CommandLine;
using CommandLine.Text;
using GenericParsing;

#endregion

namespace TextQL
{
    /// <summary>
    /// Class Program.
    /// </summary>
    internal class Program
    {
        #region Private fields

        /// <summary>
        /// The options
        /// </summary>
        private static TextQLOptions _options;
        /// <summary>
        /// The DataSet
        /// </summary>
        private static DataSet _dataSet;
        /// <summary>
        /// The Stopwatch (Bench utility)
        /// </summary>
        private static Stopwatch _stopwatch;

        #endregion

        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        /// <param name="args">The arguments. Source argument is required</param>
        private static void Main(string[] args)
        {
            _options = new TextQLOptions();
            if (Parser.Default.ParseArguments(args, _options))
            {
                try
                {
                    _stopwatch = new Stopwatch();
                    CheckSourceAndSaveToPath();
                    DetermineSeparator();
                    LoadDataSet();
                    BuildAndRunSQL();
                    ExitSuccess();
                }
                catch (Exception ex)
                {
                    ExitFailure(ex.Message);
                }
            }
            ExitFailure();
        }

        #region TextQL Methods

        /// <summary>
        /// Checks the source and save-to path.
        /// </summary>
        private static void CheckSourceAndSaveToPath()
        {
            var src = Path.GetFullPath(_options.Source);
            if (string.IsNullOrWhiteSpace(src))
            {
                ExitFailure("Source can't be empty");
            }
            if (!File.Exists(src))
            {
                ExitFailure("Source not found");
            }
            _options.Source = src;

            if (_options.SaveTo != ":memory:")
            {
                _options.SaveTo = _options.SaveTo.ToCleanPath();
            }
        }

        /// <summary>
        /// Determines the separator.
        /// </summary>
        private static void DetermineSeparator()
        {
            var dlm = _options.Delimiter;
            if (!string.IsNullOrEmpty(dlm))
            {
                if (dlm == "tab")
                {
                    _options.Delimiter = "\t";
                }
                else if (dlm.StartsWith("0x"))
                {
                    var buffer = new byte[dlm.Length / 2];
                    for (var i = 0; i < dlm.Length; i += 2)
                    {
                        var hexdec = dlm.Substring(i, 2);
                        buffer[i / 2] = byte.Parse(hexdec, NumberStyles.HexNumber);
                    }
                    _options.Delimiter = Encoding.UTF8.GetString(buffer);
                }
            }
            else
            {
                ExitFailure("Delimiter can't be null or empty");
            }
        }

        /// <summary>
        /// Loads the data set.
        /// </summary>
        private static void LoadDataSet()
        {
            try
            {
                _stopwatch.Start();
                using (var parser = new GenericParserAdapter(_options.Source))
                {
                    parser.ColumnDelimiter = _options.Delimiter;
                    parser.FirstRowHasHeader = _options.Header;
                    _dataSet = parser.GetDataSet();
                }

            }
            catch (Exception e)
            {
                ExitFailure(e.Message);
            }
        }

        /// <summary>
        /// Builds database and run SQL.
        /// </summary>
        private static void BuildAndRunSQL()
        {
            if (_dataSet != null
                && _dataSet.Tables.Count > 0
                && _dataSet.Tables[0].Columns.Count > 0)
            {

                var columns = _dataSet.Tables[0].Columns.Count;
                var rows = _dataSet.Tables[0].Rows.Count;
                var columnNames = new List<string>(columns);
                var tableName = SanitizeString(_options.TableName);

                for (var i = 0; i < columns; i++)
                {
                    var oldName = _dataSet.Tables[0].Columns[i].ColumnName;
                    var newName = SanitizeString(oldName);
                    if (_options.Verbose && oldName != newName)
                    {
                        WriteInfo(string.Format("Column {0} renamed to {1}", oldName, newName));
                    }
                    columnNames.Add(newName);
                }

                using (var connection = new SQLiteConnection("data source=" + _options.SaveTo))
                {
                    connection.Open();
                    using (var cmdCreateTable = new SQLiteCommand(connection))
                    {
                        cmdCreateTable.CommandText = "CREATE TABLE IF NOT EXISTS "
                                                     + SanitizeString(_options.TableName)
                                                     + " ("
                                                     + string.Join(" TEXT,", columnNames.ToArray())
                                                     + " TEXT);";
                        cmdCreateTable.ExecuteNonQuery();
                    }

                    using (var cmdInsertValues = new SQLiteCommand(connection))
                    {
                        var transaction = connection.BeginTransaction();
                        cmdInsertValues.Transaction = transaction;
                        var sbQuery = new StringBuilder();
                        for (var i = 1; i < rows; i++)
                        {
                            sbQuery.AppendLine("INSERT INTO " + tableName + " VALUES (");
                            for (var j = 0; j < columns; j++)
                            {
                                sbQuery.Append("'" + _dataSet.Tables[0].Rows[i].ItemArray[j].ToString().SafeReplace("'", "''") + "'");
                                if (j < columns - 1)
                                {
                                    sbQuery.Append(",");
                                }
                            }
                            sbQuery.Append(");");
                        }
                        cmdInsertValues.CommandText = sbQuery.ToString();
                        cmdInsertValues.ExecuteNonQuery();
                        transaction.Commit();
                    }

                    if (_options.Verbose)
                    {
                        WriteInfo(string.Format("Data loaded in {0} milliseconds", _stopwatch.ElapsedMilliseconds));
                    }


                    if (!string.IsNullOrWhiteSpace(_options.SQL))
                    {
                        _stopwatch.Restart();
                        using (var command = new SQLiteCommand(connection))
                        {
                            command.CommandText = _options.SQL;
                            Console.WriteLine(command.ExecuteScalar());
                        }
                        if (_options.Verbose)
                        {
                            WriteInfo(string.Format("Queries run in {0} milliseconds", _stopwatch.ElapsedMilliseconds));
                        }
                    }

                    if (_options.Console)
                    {
                        var appPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                        var sqliteExecutable = appPath + "\\sqlite3.exe";
                        
                        if (File.Exists(sqliteExecutable))
                        {
                            string dbPath;

                            if (_options.SaveTo == ":memory:")
                            {
                                var tempPath = appPath + "\\temp.db";
                                if (File.Exists(tempPath))
                                {
                                    File.Delete(tempPath);
                                }
                                var tempConnection = new SQLiteConnection("data source=" + tempPath);
                                tempConnection.Open();
                                connection.BackupDatabase(tempConnection, "main", "main", -1, null, 0);

                                dbPath = tempPath;
                            }
                            else
                            {
                                dbPath = _options.SaveTo;
                            }
                            
                            var sqliteProcess = new Process
                            {
                                StartInfo =
                                {
                                    FileName = sqliteExecutable,
                                    Arguments = dbPath
                                }
                            };

                            sqliteProcess.StartInfo.UseShellExecute = false;
                            sqliteProcess.StartInfo.RedirectStandardOutput = true;
                            sqliteProcess.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
                            sqliteProcess.Start();
                            sqliteProcess.BeginOutputReadLine();
                            sqliteProcess.WaitForExit();
                        }
                        else
                        {
                            ExitFailure("An error occured launching sqlite3 shell");
                        }
                    }
                }

            }
            else
            {
                ExitFailure("Source File is empty");
            }
        }

        #endregion

        #region Application Close Methods

        /// <summary>
        /// Exits with success code
        /// </summary>
        private static void ExitSuccess()
        {
            #if DEBUG
                Console.ReadLine();
            #endif
            Environment.Exit(0);
        }
        /// <summary>
        /// Exits with failure code and write the reason.
        /// </summary>
        /// <param name="reason">The reason.</param>
        private static void ExitFailure(string reason = "")
        {
            WriteError(reason);
            #if DEBUG
                Console.ReadLine();
            #endif
            Environment.Exit(1);
        }

        #endregion

        #region Class : TextQL Options

        /// <summary>
        /// Class TextQLOptions.
        /// </summary>
        private class TextQLOptions
        {
            /// <summary>
            /// Gets or sets the source file.
            /// </summary>
            /// <value>The source file path.</value>
            [Option('s', "source",
                DefaultValue = "",
                Required = true,
                HelpText = "Source file to load")]
            public string Source { get; set; }

            /// <summary>
            /// Gets or sets the delimiter between fields.
            /// </summary>
            /// <value>The delimiter.</value>
            [Option('d', "dlm",
                DefaultValue = ",",
                HelpText = "Delimiter between fields -dlm=tab for tab, -dlm=0x## to specify a character code in hex")]
            public string Delimiter { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the source file has header.
            /// </summary>
            /// <value><c>true</c> if header; otherwise, <c>false</c>.</value>
            [Option('h', "header",
                DefaultValue = false,
                HelpText = "Treat file as having the first row as a header row")]
            public bool Header { get; set; }

            /// <summary>
            /// Gets or sets the save-to parameter path.
            /// </summary>
            /// <value>The save to.</value>
            [Option("save-to",
                DefaultValue = ":memory:",
                HelpText = "If set, sqlite3 db is left on disk at this path")]
            public string SaveTo { get; set; }

            /// <summary>
            /// Gets or sets the name of the table.
            /// </summary>
            /// <value>The name of the table.</value>
            [Option('t',"table-name",
                DefaultValue = "tbl",
                HelpText = "Override the default table name")]
            public string TableName { get; set; }

            /// <summary>
            /// Gets or sets the SQL Command(s) to run on the data.
            /// </summary>
            /// <value>The SQL.</value>
            [Option('q',"sql",
                DefaultValue = "",
                HelpText = "SQL Command(s) to run on the data")]
            public string SQL { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether after all commands are run, open sqlite3 console with this data.
            /// </summary>
            /// <value><c>true</c> if console; otherwise, <c>false</c>.</value>
            [Option('c', "console",
                DefaultValue = false,
                HelpText = "After all commands are run, open sqlite3 console with this data")]
            public bool Console { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether textQL is in verbose.
            /// </summary>
            /// <value><c>verbose mode activated</c> if verbose; otherwise, <c>verbose mode deactivated</c>.</value>
            [Option('v', "verbose",
                DefaultValue = false,
                HelpText = "Enable verbose logging")]
            public bool Verbose { get; set; }

            
            /// <summary>
            /// Gets the usage of TextQL.
            /// </summary>
            /// <returns>System.String.</returns>
            [HelpOption]
            public string GetUsage()
            {
                return HelpText.AutoBuild(this,
                    current => HelpText.DefaultParsingErrorsHandler(this, current));
            }
        }

        #endregion

        #region Utils

        /// <summary>
        /// Sanitizes the string.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>System.String.</returns>
        private static string SanitizeString(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                //Remove all accents
                var bytes = Encoding.GetEncoding("Cyrillic").GetBytes(value);

                value = Encoding.ASCII.GetString(bytes);

                //Replace spaces 
                value = Regex.Replace(value, @"\s", "-", RegexOptions.Compiled);

                //Remove invalid chars 
                value = Regex.Replace(value, @"[^\w\s\p{Pd}]", "", RegexOptions.Compiled);

                //Trim dashes from end 
                value = value.Trim('-', '_');

                //Replace double occurences of - or \_ 
                value = Regex.Replace(value, @"([-_]){2,}", "$1", RegexOptions.Compiled);
            }

            return value;
        }

        /// <summary>
        /// Writes an information.
        /// </summary>
        /// <param name="text">The text.</param>
        private static void WriteInfo(string text)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        /// <summary>
        /// Writes an error.
        /// </summary>
        /// <param name="text">The text.</param>
        private static void WriteError(string text)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        #endregion
    }
}