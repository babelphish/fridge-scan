using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Structure;
using System.Drawing;
using System.Drawing.Imaging;

namespace Microsoft.Samples.Kinect.CoordinateMappingBasics
{
    class Frame
    {
        protected byte[] colorData = null;
        protected ulong[] depthData = null;

        protected FrameHistory parentHistory = null;

        public Frame(FrameHistory parentHistory, byte[] colorData, ulong[] depthData)
        {
            this.colorData = colorData;
            this.depthData = depthData;
            this.parentHistory = parentHistory;
        }

        public Bitmap GetComparisonDepthBitmap(ulong[] maxDepthMask)
        {
            if (this.depthData.Length != maxDepthMask.Length)
            {
                return null;
            }

            ushort bytesPerPixel = 4;

            IntPtr bitmapData = new IntPtr();
            Bitmap tempBitmap = new Bitmap((int)parentHistory.FrameWidth, (int)parentHistory.FrameHeight, (int)(parentHistory.FrameWidth * bytesPerPixel), System.Drawing.Imaging.PixelFormat.Format32bppArgb, bitmapData);
            BitmapData bmpData = tempBitmap.LockBits(new Rectangle(0, 0, (int)parentHistory.FrameWidth, (int)parentHistory.FrameHeight), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            ulong byteIndex = 0;
            uint depthIndex = 0;
            byte[] tempDepthMask = new byte[(maxDepthMask.Length * bytesPerPixel)];
            while (depthIndex < this.depthData.Length)
            {
                if (maxDepthMask[depthIndex] > this.depthData[depthIndex])
                {
                    tempDepthMask[byteIndex] = 0xff;
                    tempDepthMask[++byteIndex] = 0xff;
                    tempDepthMask[++byteIndex] = 0xff;
                }
                else
                {
                    byteIndex += 2;
                }
                tempDepthMask[++byteIndex] = 0xff;
                byteIndex++; //fourth increment
                depthIndex++;
            }
            System.Runtime.InteropServices.Marshal.Copy(tempDepthMask, 0, bitmapData, tempDepthMask.Length);
            tempBitmap.UnlockBits(bmpData);

            return tempBitmap;
        }

        private bool LoadDepthPoints(string filePath)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(long[]));

            if (File.Exists(filePath))
            {
                using (FileStream fs = File.OpenRead(filePath))
                {
                    this.depthData = (ulong[])serializer.ReadObject(fs);
                }
                return true;
            }
            else
            {
                Array.Clear(this.depthData, 0, this.depthData.Length);
                return false;
            }
        }

        public virtual ulong[] GetDepthData()
        {
            return this.depthData;
        }

        public virtual void SaveClosedDepthPoints(string filePath)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(long[]));

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            using (FileStream fs = File.Create(filePath))
            {
                serializer.WriteObject(fs, GetDepthData());
            }
        }
    }
}
