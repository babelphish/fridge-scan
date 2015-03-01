using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Samples.Kinect.CoordinateMappingBasics
{
    class AverageDepthFrame : Frame
    {
        ushort _minOutlier;
        ushort _maxOutlier;

        ushort[] _iterations;

        public AverageDepthFrame(byte[] colorData, ulong[] depthData, ushort minOutlier, ushort maxOutlier)
            : base(null, colorData, depthData)
        {
            this._minOutlier = minOutlier;
            this._maxOutlier = maxOutlier;

            _iterations = new ushort[depthData.Length];
        }

        public override ulong[] GetDepthData()
        {
            ulong[] averagedDepthData = new ulong[depthData.Length];

            for (int i = 0; i < depthData.Length; i++)
            {
                if (this._iterations[i] == 0)
                {
                    averagedDepthData[i] = 0; //don't want to divide by zero
                }
                else
                {
                    averagedDepthData[i] = this.depthData[i] / _iterations[i];
                }
            }
            
            return averagedDepthData;
        }

        public bool AddFrameToAverage(byte[] newColorData, ulong[] depthToAdd)
        {
            if (depthToAdd.Length == this.depthData.Length)
            {
                for (int i = 0; i < depthData.Length; i++)
                {
                    ulong addedDepth = depthToAdd[i];
                    if (addedDepth != 0 && addedDepth > _minOutlier && addedDepth < _maxOutlier)
                    {
                        if (this._iterations[i] < 100)
                        {
                            this.depthData[i] += addedDepth;
                            this._iterations[i]++;
                        }
                    }
                }
                this.colorData = newColorData;
                return true;
            }
            else
            {
                return false;
            }
        }

    }
}
