using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Adamantite.GFX
{
    public static class CanvasTextHelper
    {
        private const int GlyphWidth = 5;
        private const int GlyphHeight = 7;
        private const int GlyphSpacing = 1;

        private static readonly Dictionary<char, byte[]> Glyphs = new()
        {
            ['A'] = new byte[] { 0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 },
            ['B'] = new byte[] { 0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110 },
            ['C'] = new byte[] { 0b01110, 0b10001, 0b10000, 0b10000, 0b10000, 0b10001, 0b01110 },
            ['D'] = new byte[] { 0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11110 },
            ['E'] = new byte[] { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111 },
            ['F'] = new byte[] { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000 },
            ['G'] = new byte[] { 0b01110, 0b10001, 0b10000, 0b10111, 0b10001, 0b10001, 0b01110 },
            ['H'] = new byte[] { 0b10001, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 },
            ['I'] = new byte[] { 0b01110, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 },
            ['J'] = new byte[] { 0b00111, 0b00010, 0b00010, 0b00010, 0b00010, 0b10010, 0b01100 },
            ['K'] = new byte[] { 0b10001, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010, 0b10001 },
            ['L'] = new byte[] { 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b11111 },
            ['M'] = new byte[] { 0b10001, 0b11011, 0b10101, 0b10101, 0b10001, 0b10001, 0b10001 },
            ['N'] = new byte[] { 0b10001, 0b11001, 0b10101, 0b10011, 0b10001, 0b10001, 0b10001 },
            ['O'] = new byte[] { 0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 },
            ['P'] = new byte[] { 0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, 0b10000 },
            ['Q'] = new byte[] { 0b01110, 0b10001, 0b10001, 0b10001, 0b10101, 0b10010, 0b01101 },
            ['R'] = new byte[] { 0b11110, 0b10001, 0b10001, 0b11110, 0b10100, 0b10010, 0b10001 },
            ['S'] = new byte[] { 0b01111, 0b10000, 0b10000, 0b01110, 0b00001, 0b00001, 0b11110 },
            ['T'] = new byte[] { 0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100 },
            ['U'] = new byte[] { 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 },
            ['V'] = new byte[] { 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100 },
            ['W'] = new byte[] { 0b10001, 0b10001, 0b10001, 0b10101, 0b10101, 0b10101, 0b01010 },
            ['X'] = new byte[] { 0b10001, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001, 0b10001 },
            ['Y'] = new byte[] { 0b10001, 0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100 },
            ['Z'] = new byte[] { 0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b11111 },
            ['0'] = new byte[] { 0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110 },
            ['1'] = new byte[] { 0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 },
            ['2'] = new byte[] { 0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b01000, 0b11111 },
            ['3'] = new byte[] { 0b11110, 0b00001, 0b00001, 0b01110, 0b00001, 0b00001, 0b11110 },
            ['4'] = new byte[] { 0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010 },
            ['5'] = new byte[] { 0b11111, 0b10000, 0b10000, 0b11110, 0b00001, 0b00001, 0b11110 },
            ['6'] = new byte[] { 0b01110, 0b10000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110 },
            ['7'] = new byte[] { 0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000 },
            ['8'] = new byte[] { 0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110 },
            ['9'] = new byte[] { 0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00001, 0b01110 },
            [' '] = new byte[] { 0, 0, 0, 0, 0, 0, 0 },
            ['.'] = new byte[] { 0, 0, 0, 0, 0, 0b00100, 0b00100 },
            [','] = new byte[] { 0, 0, 0, 0, 0b00100, 0b00100, 0b01000 },
            [':'] = new byte[] { 0, 0b00100, 0b00100, 0, 0b00100, 0b00100, 0 },
            [';'] = new byte[] { 0, 0b00100, 0b00100, 0, 0b00100, 0b00100, 0b01000 },
            ['-'] = new byte[] { 0, 0, 0, 0b11111, 0, 0, 0 },
            ['='] = new byte[] { 0, 0b11111, 0, 0b11111, 0, 0, 0 },
            ['/'] = new byte[] { 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0, 0 },
            ['['] = new byte[] { 0b01110, 0b01000, 0b01000, 0b01000, 0b01000, 0b01000, 0b01110 },
            [']'] = new byte[] { 0b01110, 0b00010, 0b00010, 0b00010, 0b00010, 0b00010, 0b01110 },
            ['('] = new byte[] { 0b00010, 0b00100, 0b01000, 0b01000, 0b01000, 0b00100, 0b00010 },
            [')'] = new byte[] { 0b01000, 0b00100, 0b00010, 0b00010, 0b00010, 0b00100, 0b01000 },
            ['+'] = new byte[] { 0, 0b00100, 0b00100, 0b11111, 0b00100, 0b00100, 0 },
            ['!'] = new byte[] { 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0, 0b00100 },
            ['?'] = new byte[] { 0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0, 0b00100 },
            ['_'] = new byte[] { 0, 0, 0, 0, 0, 0, 0b11111 },
            ['"'] = new byte[] { 0b01010, 0b01010, 0, 0, 0, 0, 0 },
            ['\\'] = new byte[] { 0b10000, 0b01000, 0b00100, 0b00010, 0b00001, 0, 0 }
        };

        public static void Prin(Canvas c, int x, int y, string text, Color color)
        {
            if (c == null || string.IsNullOrEmpty(text)) return;

            int cursorX = x;
            int cursorY = y;

            foreach (char raw in text)
            {
                if (raw == '\n')
                {
                    cursorX = x;
                    cursorY += GlyphHeight + GlyphSpacing;
                    continue;
                }

                char ch = char.ToUpperInvariant(raw);
                if (!Glyphs.TryGetValue(ch, out var glyph))
                {
                    glyph = Glyphs['?'];
                }

                for (int row = 0; row < GlyphHeight; row++)
                {
                    byte rowBits = glyph[row];
                    for (int col = 0; col < GlyphWidth; col++)
                    {
                        int mask = 1 << (GlyphWidth - 1 - col);
                        if ((rowBits & mask) != 0)
                        {
                            c.SetPixel(cursorX + col, cursorY + row, color);
                        }
                    }
                }

                cursorX += GlyphWidth + GlyphSpacing;
            }
        }
    }
}
