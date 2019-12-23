﻿namespace TraSH
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using TraSH.Builtins;
    using TraSH.Buffer;

    public class LineEditor
    {
        public EventHandler<string> LineReceived;

        private readonly HistoryManager historyManager;
        private readonly StringBuilder buffer;
        private readonly IReadOnlyDictionary<ConsoleKey, Action<ConsoleKeyInfo>> consoleKeyMap;
        private readonly Cursor cursor;
        private IConsoleBuffer consoleBuffer;
        private IEnumerator<string> suggestionCache;

        private bool useSuggestionCache;
        private string bufferCache;

        public LineEditor()
        {
            this.historyManager = new HistoryManager();
            this.buffer = new StringBuilder();
            this.consoleKeyMap = new Dictionary<ConsoleKey, Action<ConsoleKeyInfo>>
            {
                { ConsoleKey.Enter, this.HandleEnter },
                { ConsoleKey.Backspace, this.HandleBackspace },
                { ConsoleKey.Delete, this.HandleDelete },
                { ConsoleKey.LeftArrow, this.HandleLeftArrow },
                { ConsoleKey.RightArrow, this.HandleRightArrow },
                { ConsoleKey.UpArrow, this.HandleUpArrow },
                { ConsoleKey.DownArrow, this.HandleDownArrow },
                { ConsoleKey.Home,  this.HandleHome },
                { ConsoleKey.End,  this.HandleEnd },
                { ConsoleKey.V, this.HandlePaste },
                { ConsoleKey.D, this.HandleExit },
                { ConsoleKey.LeftWindows, this.IgnoreCharacter },
                { ConsoleKey.RightWindows, this.IgnoreCharacter }
            };

            this.consoleBuffer = new SingleLineConsoleBuffer(this.GetPrompt);
            this.cursor = new Cursor(this.buffer, this.GetPrompt);
            this.useSuggestionCache = false;
        }

        public void Start()
        {
            Task.Run(this.historyManager.Start);

            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;
                Console.WriteLine();
                this.buffer.Clear();
                this.PrintPrompt();
            };

            while (true)
            {
                ConsoleKeyInfo c = Console.ReadKey(true);
                if (this.consoleKeyMap.ContainsKey(c.Key))
                {
                    this.consoleKeyMap[c.Key](c);
                }
                else
                {
                    this.HandleCharacter(c);
                }
            }
        }

        public void PrintPrompt()
        {
            //Console.ForegroundColor = ConsoleColor.Green;
            //Console.Write(this.GetPrompt());
            //Console.ResetColor();
        }

        private void HandleEnter(ConsoleKeyInfo c)
        {
            Console.WriteLine();
            if (!this.consoleBuffer.IsEmpty)
            {
                //string newline = this.buffer.ToString();
                string newline = this.consoleBuffer.GetContent();
                this.historyManager.Add(newline);
                this.LineReceived?.Invoke(this, newline);
                //this.buffer.Clear();
            }
            //else
            //{
            //    this.PrintPrompt();
            //}
            this.consoleBuffer = new SingleLineConsoleBuffer(this.GetPrompt);
        }

        private void HandleBackspace(ConsoleKeyInfo c)
        {
            if (this.buffer.Length > 0 && this.cursor.RelativePosition > 0)
            {
                int delPosition = this.cursor.RelativePosition - 1;
                int delCount = this.buffer.Length - this.cursor.RelativePosition;
                this.buffer.Remove(delPosition, 1);
                this.cursor.MoveLeft();
                this.ReplaceBufferAndMoveCursor(delCount + 1);
            }
        }

        private void HandleDelete(ConsoleKeyInfo c)
        {
            if (this.buffer.Length > 0 && this.cursor.RelativePosition < this.buffer.Length)
            {
                int delPosition = this.cursor.RelativePosition;
                int delCount = this.buffer.Length - this.cursor.RelativePosition;
                this.buffer.Remove(delPosition, 1);
                this.ReplaceBufferAndMoveCursor(delCount);
            }
        }

        private void ReplaceBufferAndMoveCursor(int delCount)
        {
            for (int i = this.cursor.RelativePosition; i < this.buffer.Length; i++)
            {
                Console.Write(this.buffer[i]);
            }

            Console.Write(" ");
            this.cursor.MoveLeft(delCount);
        }

        private void HandleLeftArrow(ConsoleKeyInfo c)
        {
            //this.cursor.MoveLeft();
            this.consoleBuffer.MoveCursorLeft(1);
        }

        private void HandleRightArrow(ConsoleKeyInfo c)
        {
            //this.cursor.MoveRight();
            this.consoleBuffer.MoveCursorRight(1);
        }

        private void HandleUpArrow(ConsoleKeyInfo c)
        {
            if (!this.useSuggestionCache)
            {
                this.bufferCache = this.buffer.ToString();
                this.suggestionCache = this.historyManager.AutoComplete(this.bufferCache).GetEnumerator();
                this.useSuggestionCache = true;
            }

            if (!this.suggestionCache.MoveNext())
            {
                this.suggestionCache = this.historyManager.AutoComplete(this.bufferCache).GetEnumerator();
            }
            string suggestion = this.suggestionCache.Current;
            if (!string.IsNullOrEmpty(suggestion))
            {
                suggestion = suggestion.Substring(this.bufferCache.Length, suggestion.Length - this.bufferCache.Length);
            }

            this.ClearBuffer();
            this.InsertStringAtCursor(this.bufferCache);
            this.InsertStringAtCursor(suggestion);
        }

        private void HandleDownArrow(ConsoleKeyInfo c)
        {

        }

        private void HandleHome(ConsoleKeyInfo c)
        {
            //this.cursor.MoveHome();
            this.consoleBuffer.MoveCursorLeft(int.MaxValue);
        }

        private void HandleEnd(ConsoleKeyInfo c)
        {
            this.cursor.MoveEnd();
        }

        private void HandlePaste(ConsoleKeyInfo c)
        {
            if ((c.Modifiers & ConsoleModifiers.Control) != 0)
            {
                string clipboardText = TextCopy.Clipboard.GetText();
                //this.InsertStringAtCursor(clipboardText);
                this.consoleBuffer.Write(clipboardText);
            }
            else
            {
                this.HandleCharacter(c);
            }
        }

        private void HandleExit(ConsoleKeyInfo c)
        {
            if ((c.Modifiers & ConsoleModifiers.Control) != 0)
            {
                if (this.buffer.Length == 0)
                {
                    BuiltInCommand exit = new Exit();
                    Console.WriteLine();
                    exit.Execute(Enumerable.Empty<string>());
                }
                else if (this.cursor.RelativePosition < this.buffer.Length)
                {
                    this.HandleDelete(c);
                }
            }
            else
            {
                this.HandleCharacter(c);
            }
        }

        private void HandleCharacter(ConsoleKeyInfo c)
        {
            this.useSuggestionCache = false;
            this.consoleBuffer.PutChar(c.KeyChar);
            //this.InsertStringAtCursor(c.KeyChar.ToString());
        }

        private void IgnoreCharacter(ConsoleKeyInfo c) { }

        private void InsertStringAtCursor(string text)
        {
            string remainingBufText = this.buffer.ToString(this.cursor.RelativePosition, this.buffer.Length - this.cursor.RelativePosition);
            this.buffer.Insert(this.cursor.RelativePosition, text);

            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write(text);
            Console.Write(remainingBufText);
            Console.ResetColor();

            this.cursor.MoveLeft(remainingBufText.Length);
        }

        private void ClearBuffer()
        {
            this.cursor.MoveHome();
            for (int i = 0; i < this.buffer.Length; i++)
            {
                Console.Write(" ");
            }
            this.buffer.Clear();
            this.cursor.MoveHome();
        }

        private string GetPrompt()
        {
            return $"{Environment.MachineName}:{new DirectoryInfo(Environment.CurrentDirectory).Name} {Console.WindowTop} {Console.CursorTop}> ";
        }

        private class Cursor
        {
            private readonly StringBuilder buffer;
            private readonly Func<string> getPromptFunc;

            public Cursor(StringBuilder buffer, Func<string> getPromptFunc)
            {
                this.getPromptFunc = getPromptFunc;
                this.buffer = buffer;
            }

            public int RelativePosition { get => Console.CursorLeft - this.PromptLength; }

            public int Position { get => Console.CursorLeft; }

            public void MoveLeft()
            {
                this.MoveLeft(1);
            }

            public void MoveLeft(int count)
            {
                if (this.RelativePosition > 0)
                {
                    Console.SetCursorPosition(Math.Max(this.Position - count, this.PromptLength), Console.CursorTop);
                }
            }

            public void MoveRight()
            {
                this.MoveRight(1);
            }

            public void MoveRight(int count)
            {
                if (this.RelativePosition < this.buffer.Length)
                {
                    Console.SetCursorPosition(Math.Min(this.Position + count, this.buffer.Length + this.PromptLength), Console.CursorTop);
                }
            }

            public void MoveHome()
            {
                Console.SetCursorPosition(this.PromptLength, Console.CursorTop);
            }

            public void MoveEnd()
            {
                Console.SetCursorPosition(this.PromptLength + this.buffer.Length, Console.CursorTop);
            }

            private int PromptLength { get => this.getPromptFunc().Length; }
        }
    }
}
