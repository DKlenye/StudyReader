using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using Newtonsoft.Json;

namespace StudyReader
{
    class Program
    {

        static readonly string pathToRepository = @"C:\_dev\suvoda-services.IRT\";
        static readonly string[] excludedBranches = { "origin/HEAD", "origin/master" };
        static readonly string modulesPattern = ".*modules[\\\\]suvoda[.]irt[.]modules.*[.]dll";
        private static readonly string jsonPath = "StudyModules.json";
        private static readonly string sqlPath = "StudyModules.sql";

        static void Main(string[] args)
        {
            var info = ReadFromRepository();
            WriteToJson(info);
            WriteToSql(info);
        }

        static void WriteToSql(IEnumerable<Study> studies)
        {
            var builder = new StringBuilder();

            builder.AppendLine(@"
            CREATE TABLE [dbo].[StudyModules](
	            [Study] [nvarchar](250) NULL,
	            [Module] [nvarchar](250) NULL,
                [IsCustomized] [bit] NULL
            ) ON [PRIMARY]")
            .AppendLine("GO");

            studies.ToList().ForEach(s =>
            {
                s.Modules.ToList().ForEach(m =>
                {
                    builder.AppendLine(string.Format(
                        @"INSERT [dbo].[StudyModules] ([Study], [Module], [IsCustomized]) VALUES (N'{0}', N'{1}', {2})", s.Name, m.Name, m.IsCustomized ? 1 : 0))
                    .AppendLine("GO");
                });
            });

            File.WriteAllText(sqlPath, builder.ToString());
        }

        static void WriteToJson(IEnumerable<Study> studyInfo)
        {
            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(studyInfo, Formatting.Indented));
        }

        static IEnumerable<Study> ReadfromJson()
        {
            var json = File.ReadAllText(jsonPath);
            return JsonConvert.DeserializeObject<List<Study>>(json);
        }

        static IEnumerable<Study> ReadFromRepository()
        {
            using (var repo = new Repository(pathToRepository))
            {
                var remoteBranches = repo.Branches.Where(x => x.IsRemote && !excludedBranches.Contains(x.FriendlyName)).ToList();
                var counter = remoteBranches.Count;

                Console.WriteLine("Branch count - {0}", counter);

                return remoteBranches.Select(branch =>
                {
                    Commands.Checkout(repo, branch);
                    Console.WriteLine("{0}-{1}", counter--, branch.FriendlyName);
                    return new Study
                    {
                        Name = GetStudyName(branch.FriendlyName),
                        Modules = FindModules(repo)
                    };
                }).ToList();
            }
        }

        static IEnumerable<Module> FindModules(Repository repo)
        {
            var modulesFindRegex = new Regex(modulesPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            return repo.Index.ToList()
                .Where(x => modulesFindRegex.IsMatch(x.Path))
                .Select(x =>
                {
                    Console.WriteLine("\t\t" + x.Path);
                    return GetModuleName(x.Path);
                })
                .Distinct()
                .Select(x =>
                {
                    Console.WriteLine("\t" + x);
                    return new Module {Name = x, IsCustomized = CheckModuleIsCustomized(x)};
                }).ToList();
        }

        static string GetModuleName(string path)
        {
            return path.Split('\\').Last()
                .Replace(".dll", "")
                .Replace("Suvoda.IRT.Modules.", "");
        }

        static string GetStudyName(string branchName)
        {
            return branchName.Replace("origin/","");
        }

        static bool CheckModuleIsCustomized(string moduleName)
        {
            return moduleName.Contains("_") || 
                moduleName.Contains("-") || 
                moduleName.ToLower().Contains("custom");
        }
    }
}
