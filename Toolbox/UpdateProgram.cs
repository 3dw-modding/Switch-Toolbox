﻿using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Toolbox.Library;

namespace Toolbox {
    public class UpdateProgram {
        static List<Release> Releases = new List<Release>();
        public static bool CanUpdate = false;
        public static bool Downloaded = false;
        public static Release LatestRelease;
        public static List<GitHubCommit> CommitList = new List<GitHubCommit>();
        public static DateTime LatestReleaseTime;


        public static void CheckLatest()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var client = new GitHubClient(new ProductHeaderValue("ST_UpdateTool"));

                GetReleases(client).Wait();

                var info = client.GetLastApiInfo();
                if (info != null && info.RateLimit.Remaining <= 0)
                    return;

                GetCommits(client).Wait();

                var asssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
                var version = string.Concat(asssemblyVersion.ToString().Reverse().Skip(2).Reverse());

                var lastestRelease = Releases.FirstOrDefault(x => x.Name.Contains($"v{version}"));
                if (lastestRelease != null)
                {
                    Runtime.ProgramVersion = lastestRelease.TagName;
                    Runtime.CompileDate = lastestRelease.Assets[0].UpdatedAt.ToString();
                    Runtime.CommitInfo = lastestRelease.TargetCommitish;
                    return;
                }

                foreach (Release latest in Releases)
                {
                    if (latest.Assets?.Count == 0)
                        continue;

                    Console.WriteLine(
                        "The latest release is tagged at {0} and is named {1} commit {2} date {3}",
                        latest.TagName,
                        latest.Name,
                        latest.TargetCommitish,
                        latest.Assets[0].UpdatedAt.ToString());

                    LatestReleaseTime = latest.Assets[0].UpdatedAt.DateTime;
                    LatestRelease = latest;
                    CanUpdate = true;

                    break;
                }

                Releases.Clear();
            }
            catch (Exception) { }
        }

        static void DownloadRelease()
        {
            ProcessStartInfo p = new ProcessStartInfo();
            p.WindowStyle = ProcessWindowStyle.Hidden;
            p.CreateNoWindow = true;
            p.FileName = Path.Combine(Runtime.ExecutableDir, "Updater.exe");
            p.WorkingDirectory = Path.Combine(Runtime.ExecutableDir, "updater/");
            Console.WriteLine($"Updater: {p.FileName}");
            p.Arguments = "-d";

            Process process = new Process();
            process.StartInfo = p;
            Console.WriteLine("Downloading...");
            process.Start();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new TimeoutException();
            Console.WriteLine("Finished downloading");
            string updateExe = Path.Combine(Runtime.ExecutableDir, "master\\Toolbox.exe"),
                  currentExe = System.Reflection.Assembly.GetEntryAssembly().Location;
            if (!Utils.CreateMD5Hash(currentExe).SequenceEqual(Utils.CreateMD5Hash(updateExe)))
                CanUpdate = true;
        }

        static async Task GetCommits(GitHubClient client)
        {
            var options = new ApiOptions
            {
                PageSize = 20,
                PageCount = 1
            };

            DateTimeOffset CurrentRelease;
            bool IsValidTime = DateTimeOffset.TryParse(Runtime.CompileDate, out CurrentRelease);

            foreach (GitHubCommit c in await client.Repository.Commit.GetAll("KillzXGaming", "Switch-Toolbox", options))
            {
                if (IsValidTime)
                {
                    if (!(CurrentRelease.DateTime < c.Commit.Author.Date.DateTime))
                        break;
                    CommitList.Add(c);
                }
                else
                    //Just add extra commits. This shouldn't happen unless the user actually edits the file
                    CommitList.Add(c);
            }
        }

        static async Task GetReleases(GitHubClient client)
        {
            Releases = new List<Release>();
            foreach (Release r in await client.Repository.Release.GetAll("KillzXGaming", "Switch-Toolbox"))
                Releases.Add(r);
        }
    }
}