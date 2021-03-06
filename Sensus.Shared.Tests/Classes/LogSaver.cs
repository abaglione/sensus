﻿// Copyright 2014 The Rector & Visitors of the University of Virginia
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.IO;
using System.Text;

namespace Sensus.Tests.Classes
{
    public class LogSaver : TextWriter
    {
        public override Encoding Encoding => Encoding.Unicode;

        public StringBuilder Log { get; } = new StringBuilder();

        public override void Write(string value)
        {
            base.Write(value);

            lock (Log)
            {
                Log.Append(value);
            }
        }

        public override void WriteLine()
        {
            base.WriteLine();

            lock (Log)
            {
                Log.AppendLine();
            }
        }

        public override void WriteLine(string value)
        {
            base.WriteLine(value);

            lock (Log)
            {
                Log.AppendLine(value);
            }
        }
    }
}
