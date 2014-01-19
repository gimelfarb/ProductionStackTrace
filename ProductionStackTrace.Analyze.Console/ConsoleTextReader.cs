using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductionStackTrace.Analyze.Console
{
    using Console = System.Console;
    public class ConsoleTextReader : TextReader
    {
        private int _bufChar = -1;
        private bool _escapeDetected;

        public bool IsEscapeDetected { get { return _escapeDetected; } }

        public override int Peek()
        {
            if (_bufChar < 0 && Console.KeyAvailable)
            {
                _bufChar = Console.ReadKey(true).KeyChar;
                return _bufChar;
            }
            return _bufChar;
        }

        public override int Read()
        {
            var c = _bufChar;
            if (c >= 0) 
            { 
                _bufChar = -1; 
                return c; 
            }

            if (!Console.KeyAvailable) return -1;

            var k = Console.ReadKey(true);
            if (k.Key == ConsoleKey.Escape)
            {
                _escapeDetected = true;
                return -1;
            }

            return k.KeyChar;
        }
    }
}
