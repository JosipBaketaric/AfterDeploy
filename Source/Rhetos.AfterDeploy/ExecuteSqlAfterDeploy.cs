﻿/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using Rhetos.Deployment;
using Rhetos.Extensibility;
using Rhetos.Logging;
using Rhetos.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rhetos.AfterDeploy
{
    [Export(typeof(IServerInitializer))]
    public class ExecuteSqlAfterDeploy : IServerInitializer
    {
        private readonly IInstalledPackages _installedPackages;
        private readonly ISqlExecuter _sqlExecuter;
        private readonly ILogger _logger;
        private readonly ILogger _deployPackagesLogger;

        public ExecuteSqlAfterDeploy(IInstalledPackages installedPackages, ISqlExecuter sqlExecuter, ILogProvider logProvider)
        {
            _installedPackages = installedPackages;
            _sqlExecuter = sqlExecuter;
            _logger = logProvider.GetLogger("AfterDeploy");
            _deployPackagesLogger = logProvider.GetLogger("DeployPackages");
        }

        public IEnumerable<string> Dependencies
        {
            get
            {
                return new[]
                {
                    "Rhetos.Dom.DefaultConcepts.ClaimGenerator",
                    "Rhetos.AspNetFormsAuth.AuthenticationDatabaseInitializer"
                };
            }
        }

        public void Initialize()
        {
            // The packages are sorted by their dependencies, so the sql scripts will be executed in the same order.
            var scripts = _installedPackages.Packages
                .SelectMany(p => GetScripts(p))
                .ToList();

            foreach (var script in scripts)
            {
                _logger.Trace("Executing script " + script.Package.Id + ": " + script.Name);
                string sql = File.ReadAllText(script.Path, Encoding.UTF8);

                var sqlBatches = SqlTransactionBatch.GroupByTransaction(SqlUtility.SplitBatches(sql));
                foreach (var sqlBatch in sqlBatches)
                    _sqlExecuter.ExecuteSql(sqlBatch, sqlBatch.UseTransacion);
            }

            _deployPackagesLogger.Trace("Executed " + scripts.Count + " after-deploy scripts.");
        }

        class Script
        {
            public InstalledPackage Package;
            public string Path;
            public string Name;
        }

        /// <summary>
        /// Returns after-deploy scripts, ordered by natural sort of file paths inside each package.
        /// </summary>
        private List<Script> GetScripts(InstalledPackage package)
        {
            string afterDeployFolder = Path.GetFullPath(Path.Combine(package.Folder, "AfterDeploy"));
            if (!Directory.Exists(afterDeployFolder))
                return new List<Script> { };

            var files = Directory.GetFiles(afterDeployFolder, "*.*", SearchOption.AllDirectories)
                .OrderBy(path => CsUtility.GetNaturalSortString(path).Replace(@"\", @" \"));

            const string expectedExtension = ".sql";
            var badFile = files.FirstOrDefault(file => Path.GetExtension(file).ToLower() != expectedExtension);
            if (badFile != null)
                throw new FrameworkException("After-deploy script '" + badFile + "' does not have the expected extension '" + expectedExtension + "'.");

            return files.Select(path => new Script
                {
                    Package = package,
                    Path = path,
                    Name = GetSimpleName(path, afterDeployFolder)
                })
                .ToList();
        }

        private string GetSimpleName(string path, string folder)
        {
            string name = path.Substring(folder.Length);
            if (name.StartsWith(@"\"))
                name = name.Substring(1);
            return name;
        }
    }
}
