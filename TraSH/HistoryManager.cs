﻿namespace TraSH
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class HistoryManager
    {
        private readonly string filepath;
        private readonly OrderedHashSet<string> historySet;

        public HistoryManager()
        {
            this.filepath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".trash_history");
        }

        public HistoryManager(string filepath)
        {
            this.filepath = filepath;
            this.historySet = new OrderedHashSet<string>();
        }

        public Task Start()
        {
            return Task.Factory.StartNew(() =>
            {
                if (!File.Exists(this.filepath))
                {
                    FileStream historyFile = File.Create(this.filepath);
                    historyFile.Close();
                }

                using (StreamReader fileReader = new StreamReader(this.filepath))
                {
                    string line;
                    while ((line = fileReader.ReadLine()) != null)
                    {
                        this.historySet.Add(line);
                    }
                }
            });
        }

        public IEnumerable<string> GetHistory()
        {
            return this.historySet;
        }

        public void Add(string newLine)
        {
            if (this.historySet.Contains(newLine))
            {
                this.historySet.Remove(newLine);
                File.WriteAllLines(
                    this.filepath,
                    File.ReadLines(this.filepath).Where(l => l != newLine).ToList());
            }
            this.historySet.Add(newLine);

            using (StreamWriter fileWriter = new StreamWriter(this.filepath))
            {
                fileWriter.WriteLine(newLine);
            }
        }
    }
}
