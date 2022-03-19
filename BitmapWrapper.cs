using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEngine;
using Unity.Collections;

namespace QRCodeDecoderLibrary
{

    public class BitmapWrapper
    {
        public int Width;
        public int Height;


        // Let's immediately try to model this as a grayscale 8-bit bitmap
        public bool[] OneBitBitmap;
        public Color32[] OriginalBitmap;

        public BitmapWrapper(Color32[] input, int w, int h)
        {
            Width = w;
            Height = h;
            OriginalBitmap = input;
            OneBitBitmap = null;
        }

        public void ProcessPixels() {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            var Bitmap = new byte[OriginalBitmap.Length];
            int minGray = 255;
            int maxGray = 0;

            int index = 0;
            for (int y = Height - 1; y >= 0; y--)
            {
                var yIndex = y * Width;
                for (int x = 0; x < Width; x++)
                {
                    var c = OriginalBitmap[yIndex + x];
                    var pix = (30 * (int)(c.r) + 59 * (int)(c.g) + 11 * (int)(c.b)) / 100;
                    if (pix > maxGray)
                    {
                        maxGray = pix;
                    }
                    else if (pix < minGray)
                    {
                        minGray = pix;
                    }
                    Bitmap[index++] = (byte)pix;
                }

            }
            int cutoffLevel = (minGray + maxGray) / 2;
            OneBitBitmap = new bool[OriginalBitmap.Length];
            for (int i = 0; i < OriginalBitmap.Length; i++)
            {
                OneBitBitmap[i] = Bitmap[i] < cutoffLevel;
            }

            sw.Stop();
            //Debug.Log($"ProcessPixels did its thing in {sw.ElapsedMilliseconds} ms Color32[]({Width}x{Height} {OriginalBitmap.Length}) cutoffs: {minGray} {maxGray} {cutoffLevel}");
/*
            using (StreamWriter fs = File.CreateText($"c:\\temp\\grayscale1_{Environment.TickCount}.pgm"))
            {
                fs.Write($"P2\n{Width} {Height} 255\n");
                for (int i = 0; i < Bitmap.Length; i++)
                {
                    fs.Write($"{Bitmap[i]} ");
                    if (i % 1080 == 1079)
                    {
                        fs.Write("\n");
                    }
                }
                fs.Flush();
            }
*/
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool GetPixel(int y, int x)
        {
            return OneBitBitmap[y * Width + x];
        }

    }
}