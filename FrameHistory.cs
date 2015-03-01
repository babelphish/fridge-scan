using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Samples.Kinect.CoordinateMappingBasics
{

    class FrameHistory
    {

        private uint frameWidth;
        private uint frameHeight;
        private ushort maxFrames;

        const ushort bytesPerPixel = 4;

        Queue<Frame> frameQueue = null;

        public FrameHistory(uint frameWidth, uint frameHeight, ushort maxFrames)
        {
            this.frameWidth = frameWidth;
            this.frameHeight = frameHeight;
            this.maxFrames = maxFrames;

            this.frameQueue = new Queue<Frame>(maxFrames);
        }

        public Frame AddFrame(byte[] colorData, ulong[]depthData)
        {
            if (frameQueue.Count() >= this.maxFrames)
            {
                frameQueue.Dequeue();
            }
            Frame frameToAdd = new Frame(this, colorData, depthData);
            frameQueue.Enqueue(frameToAdd);
            return frameToAdd;
        }

        public void SaveMaxDepthMask(ushort[] depthMask)
        {

        }

        public uint FrameWidth
        {
            get { return this.frameWidth;  }
            set { this.frameWidth = value; }
        }

        public uint FrameHeight
        {
            get { return this.frameHeight;  }
            set { this.frameHeight = value;  }
        }

    }
}
