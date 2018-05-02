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

        static readonly string pathToRepository = @"C:\projects\suvoda\suvoda-services.IRT\";
        static readonly string[] excludedBranches = { "origin/HEAD", "origin/master" };
        static readonly string modulesPattern = ".*modules[\\\\]suvoda[.]irt[.]modules.*[.]dll";
        private static readonly string jsonPath = "StudyModules.json";
        private static readonly string sqlPath = "StudyModules.sql";

        static void Main(string[] args)
        {
            WriteToJson(ReadFromRepository());
        }

        static void WriteToSql(List<Study> studyList)
        {
            var builder = new StringBuilder();

            builder.Append(@"
            CREATE TABLE [dbo].[StudyModules](
	            [Study] [nvarchar](250) NULL,
	            [Module] [nvarchar](250) NULL,
                [IsCustomized] [bit] NULL
            ) ON [PRIMARY]
            GO

            ");

            studyList.ForEach(s =>
            {
                s.Modules.ToList().ForEach(m =>
                {
                    builder.AppendLine(string.Format(
                        @"INSERT [dbo].[StudyModules] ([Study], [Module], [IsCustomized]) VALUES (N'{0}', N'{1}', {2})", s.Name, m.Name, m.IsCustomized ? 1 : 0));
                    builder.AppendLine("GO");
                });
                
            });

            File.WriteAllText(sqlPath, builder.ToString());
        }

        static void WriteToJson(List<Study> studyInfo)
        {
            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(studyInfo, Formatting.Indented));
        }

        static List<Study> ReadfromJson()
        {
            var json = File.ReadAllText(jsonPath);
            return JsonConvert.DeserializeObject<List<Study>>(json);
        }

        static List<Study> ReadFromRepository()
        {
            var studyList = new List<Study>();

            using (var repo = new Repository(pathToRepository))
            {
                var remoteBranches = repo.Branches.Where(x => x.IsRemote && !excludedBranches.Contains(x.FriendlyName))
                    .ToList();
                Console.WriteLine("Branch count - {0}", remoteBranches.Count);

                var counter = remoteBranches.Count;

                remoteBranches.ForEach(branch =>
                {
                    Commands.Checkout(repo, branch);

                    var study = new Study
                    {
                        Name = GetStudyName(branch.FriendlyName),
                        Modules = new List<Module>()
                    };

                    Console.WriteLine("{0}-{1}", counter-- ,study.Name);

                    var modules = new List<string>();
                    var regex = new Regex(modulesPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                    repo.Index.ToList()
                        .Where(x => regex.IsMatch(x.Path))
                        .ToList()
                        .ForEach(x =>
                        {
                            Console.WriteLine("\t\t" + x.Path);

                            var moduleName = GetModuleName(x.Path);
                            if (!modules.Contains(moduleName))
                            {
                                modules.Add(moduleName);
                            }
                        });

                    modules.ForEach(x =>
                    {
                        Console.WriteLine("\t" + x);
                        study.Modules.Add(new Module
                        {
                            Name = x,
                            IsCustomized = CheckModuleIsCustomized(x)
                        });
                    });

                    studyList.Add(study);

                });
            }

            return studyList;
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
            return moduleName.Contains("_") || moduleName.Contains("-");
        }
        
    }
}
