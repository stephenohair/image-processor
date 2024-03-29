﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ImageProcessor {
    /// <summary>
    /// Compass edge detection original author:  Dewald Esterhuizen
    ///  
    /// Heavily modified by Stephen O'Hair for use in an experimental comparison against various operator masks.
    /// 
    /// Sourced from: https://softwarebydefault.com/2013/06/22/compass-edge-detection/
    /// </summary>
    public static class Convolution {

        // TODO - auto-generate this
        private static readonly int[] gaps = new int[7] { 0, 3, 6, 8, 10, 13, 16 };

        /// <summary>
        /// Applies the given operator against the given image and returns a new bitmap.
        /// </summary>
        /// <param name="sourceBitmap"></param>
        /// <param name="filterMatrix"></param>
        /// <param name="factor"></param>
        /// <param name="bias"></param>
        /// <returns></returns>
        public static Bitmap ConvolutionFilter(this Bitmap sourceBitmap, int numOfPixels, double[,,] filterMatrix) {
            byte[] resultBuffer = ConvolutionFilterAsBytes(sourceBitmap, numOfPixels, filterMatrix);
            Bitmap resultBitmap = new Bitmap(numOfPixels, numOfPixels);

            BitmapData resultData = resultBitmap.LockBits(new Rectangle(0, 0, resultBitmap.Width, resultBitmap.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            Marshal.Copy(resultBuffer, 0, resultData.Scan0, resultBuffer.Length);
            resultBitmap.UnlockBits(resultData);

            return resultBitmap;
        }

        /// <summary>
        /// This method originally applied the given operator against R,G and B colours in the given bitmap. I've
        /// re-worked this to adapt the same logging as Robert Cox's sample ThreeByThree textbox logging and simplified
        /// it to use only the R value as he did.
        /// </summary>
        /// <param name="sourceBitmap"></param>
        /// <param name="filterMatrix"></param>
        /// <param name="factor"></param>
        /// <param name="bias"></param>
        /// <returns></returns>
        public static byte[] ConvolutionFilterAsBytes(Bitmap sourceBitmap, int numOfPixels, double[,,] filterMatrix) {

            BitmapData sourceData = sourceBitmap.LockBits(new Rectangle(0, 0, numOfPixels, numOfPixels), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] pixelBuffer = new byte[sourceData.Stride * numOfPixels];
            byte[] resultBuffer = new byte[sourceData.Stride * numOfPixels];

            Marshal.Copy(sourceData.Scan0, pixelBuffer, 0, pixelBuffer.Length);
            sourceBitmap.UnlockBits(sourceData);

            int byteOffset = 0;
            int retv = 0;

            for (int yy = 0; yy < numOfPixels; yy++) {
                for (int xx = 0; xx < numOfPixels; xx++) {
                    retv = ComputeOperator(sourceBitmap, filterMatrix, gaps[xx], gaps[yy]);
                    resultBuffer[byteOffset] = (byte)retv;
                    resultBuffer[byteOffset + 1] = 0;
                    resultBuffer[byteOffset + 2] = 0;
                    resultBuffer[byteOffset + 3] = 255;
                    byteOffset += 4; //  
                }
            }
            return resultBuffer;
        }

        public static string ConvolutionFilterAsString(Bitmap sourceBitmap, int numOfPixels, double[,,] operators) {

            BitmapData sourceData = sourceBitmap.LockBits(new Rectangle(0, 0, numOfPixels, numOfPixels), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] pixelBuffer = new byte[sourceData.Stride * 7];

            Marshal.Copy(sourceData.Scan0, pixelBuffer, 0, pixelBuffer.Length);
            sourceBitmap.UnlockBits(sourceData);

            string retv = "";
            int computedVal = 0;

            for (int yy = 0; yy < numOfPixels; yy++) {
                for (int xx = 0; xx < numOfPixels; xx++) {
                    computedVal = ComputeOperator(sourceBitmap, operators, gaps[xx], gaps[yy]);
                    retv += computedVal + ",";
                }
            }

            return retv;
        }

        /// <summary>
        /// Moves a 3x3 operator along the given bitmap, multiplies against it, sums up the 9 values and returns that
        /// as a single paramter for a NN to train with.
        /// </summary>
        /// <returns></returns>
        private static int ComputeOperator(Bitmap bmp, double[,,] operators, int xOffset, int yOffset) {

            // holds the rotated operators and their calculated value of the pixel
            var work = new List<Tuple<int[,], int[,]>>();
            var pixelMatrix = new int[3, 3];
            // Iterate through all the directional operators and generate work.
            for (int compass = 0; compass < operators.GetLength(0); compass++) {

                int[,] currOperator = new int[3, 3];

                // go through each element in the filter and multiple the color values
                for (int y = 0; y < 3; y++) {
                    for (int x = 0; x < 3; x++) {

                        // get the red pixel
                        Color c = bmp.GetPixel(x + xOffset, y + yOffset);
                        pixelMatrix[x, y] = c.R;

                        // basically line up the current operator with it's work, must be a nicer way?
                        currOperator[x, y] = (int)operators[compass, x, y];
                    }
                }

                var operation = new Tuple<int[,], int[,]>(currOperator, pixelMatrix);
                work.Add(operation);
            }

            // list each pixel to be multiplied as a 3x3 matrix
            work.ForEach(t => {
                Form1.Instance.AppendVerboseText(ThreebythreeToString(t.Item2, "sTemp"));
            });

            // list each operator as a 3x3 matrix
            work.ForEach(t => {
                Form1.Instance.AppendVerboseText(ThreebythreeToString(t.Item1, "Operator"));
            });

            var multiplied = new List<int[,]>();
            // multiply each 3x3 operator by the 3x3 pixels
            work.ForEach(t => {
                multiplied.Add(ThreeBythreeMultiply(t.Item1, t.Item2));
            });

            // display the resultant 3x3 pixel matrix after the 3x3 operator has multiplied against it
            multiplied.ForEach(pixelMatrx => {
                Form1.Instance.AppendVerboseText(ThreebythreeToString(pixelMatrx, "sTemp"));
            });

            // now that the pixel matrices have been multiplied, sum each one up
            int mathOperator = 0;
            multiplied.ForEach(pixelMatrx => {
                var sum = ThreeBythreeSum(pixelMatrx);
                Form1.Instance.AppendVerboseText("sum=" + sum.ToString() + " ");
                mathOperator = Math.Abs(mathOperator) + Math.Abs(sum);
            });

            Form1.Instance.AppendVerboseText("Sum of operators=" + mathOperator.ToString());
            Form1.Instance.AppendVerboseText("\r\n  .........................................  \r\n");
            return mathOperator;
        }

        #region ThreeByThree Methods
        /// <summary>
        /// multiply (not a matrix multiply) two 3x3 matrixes
        /// put result back in a
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        private static int[,] ThreeBythreeMultiply(int[,] a, int[,] b) {
            for (int y = 0; y < 3; y++) {
                for (int x = 0; x < 3; x++) {
                    a[x, y] = a[x, y] * b[x, y];
                }
            }
            return a;
        }

        private static int ThreeBythreeSum(int[,] a) {
            int sum = 0;
            for (int y = 0; y < 3; y++) {
                for (int x = 0; x < 3; x++) {
                    sum = sum + a[x, y];
                }
            }
            return sum;
        }

        private static string ThreebythreeToString(int[,] a, string heading) {
            string t = "";
            if (heading != "") t = t + heading + "\r\n";
            for (int y = 0; y < 3; y++) {
                t = t + a[0, y].ToString() + " " + a[1, y].ToString() + " " + a[2, y].ToString() + "\r\n";
            }
            return t;
        }
        #endregion
    }
}
