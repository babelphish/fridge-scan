//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.CoordinateMappingBasics
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using System.Windows.Input;
    using System.Runtime.Serialization.Json;
    using ImageMagick;
    using System.Runtime.CompilerServices;
    using System.Drawing; 

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Indicates opaque in an opacity mask
        /// </summary>
        private const int OpaquePixel = -1;

        private const int startY = 500;
        private const int startX = 700;
        private const int totalWidth = 1920;
        private const int totalHeight = 1080;
        private const int widthEndReduction = 800;
        private const int heightEndReduction = 60;

        private const ushort minDepthOutlier = 1400;
        private const ushort maxDepthOutlier = 2800;

//        private int _minDepth = 

//        private float _ratio = 0.75;

        private const int reducedHeight = (totalHeight - startY) - heightEndReduction;
        private const int reducedWidth = (totalWidth - startX) - widthEndReduction;

        /// <summary>
        /// Size of the RGB pixel in the bitmap
        /// </summary>
        private readonly int bytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;

        /// <summary>
        /// Reader for depth/color/body index frames
        /// </summary>
        private MultiSourceFrameReader multiFrameSourceReader = null;

        /// <summary>
        /// Bitmap to display
        /// </summary>
        private WriteableBitmap bitmap = null;

        /// <summary>
        /// Intermediate storage for receiving depth frame data from the sensor
        /// </summary>
        private ushort[] depthFrameData = null;

        /// <summary>
        /// Intermediate storage for receiving color frame data from the sensor
        /// </summary>
        private byte[] colorFrameData = null;

        /// <summary>
        /// Intermediate storage for frame data converted to color
        /// </summary>
        private byte[] displayPixels = null;

        /// <summary>
        /// Intermediate storage for the color to depth mapping
        /// </summary>
        private DepthSpacePoint[] depthPoints = null;
        private string fridgeGifPath = @"C:\Dev\fridge_gif.gif";
        private string closedDepthPath = @"C:\Dev\closed_depth_path.json";
        private MagickImageCollection fridgeGifEncoder = null;
        private int iterationsSaved = 0;
        private ushort[] currentDepthPoints = null;
        private FrameHistory frameHistory = null;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        private long frame = 0;

        private int colorWidth = 0;
        private int colorHeight = 0;

        private bool averagingDepthPixels = true;
        private AverageDepthFrame averageDepthFrame = null;
        private ulong[] averageDepthComparison;

        private bool currentlyProcessingFrame = false;

        const int historicalFrames = 120;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // get the kinectSensor object
            this.kinectSensor = KinectSensor.GetDefault();

            // open multiframereader for depth, color, and bodyindex frames

            this.multiFrameSourceReader = this.kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color);

            // wire handler for frames arrival
            this.multiFrameSourceReader.MultiSourceFrameArrived += this.Reader_MultiSourceFrameArrived;

            // get the coordinate mapper
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // get FrameDescription from DepthFrameSource
            FrameDescription depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            int depthWidth = depthFrameDescription.Width;
            int depthHeight = depthFrameDescription.Height;

            // allocate space to put the pixels being received and converted
            this.depthFrameData = new ushort[depthWidth * depthHeight];

            // create the bitmap to display 

            // get FrameDescription from ColorFrameSource
            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.FrameDescription;

            this.colorWidth = totalWidth;
            this.colorHeight = totalHeight;

            this.bitmap = new WriteableBitmap(reducedWidth, reducedHeight, 96.0, 96.0, PixelFormats.Bgra32, null);

            this.depthPoints = new DepthSpacePoint[this.colorWidth * this.colorHeight];

            this.currentDepthPoints = new ushort[(reducedWidth) * (reducedHeight)];
            this.averageDepthComparison = new ulong[(reducedWidth * reducedHeight)];

            this.displayPixels = new byte[(reducedWidth) * (reducedHeight) * this.bytesPerPixel];

            // allocate space to put the pixels being received
            this.colorFrameData = new byte[this.colorWidth * this.colorHeight * this.bytesPerPixel];

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();

            MainImage.Width = reducedWidth;
            MainImage.Height = reducedHeight;

            this.frameHistory = new FrameHistory(reducedWidth, reducedHeight, 100);


            this.fridgeGifEncoder = new MagickImageCollection();

        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.bitmap;
            }
        }

        private void FridgeImage_MouseMove(object sender, MouseEventArgs e)
        {
            System.Windows.Point p = e.GetPosition(MainImage);
            long pixelDepth = this.currentDepthPoints[(int)((p.Y * reducedWidth) + p.X)];
            MouseData.Content = "X: " + p.X.ToString() + " Y: " + p.Y.ToString() + " Pixel: " + pixelDepth + " Current Iteration:" + this.iterationsSaved;
        }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.multiFrameSourceReader != null)
            {
                // MultiSourceFrameReder is IDisposable
                this.multiFrameSourceReader.Dispose();
                this.multiFrameSourceReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
            
            if (this.fridgeGifEncoder != null)
            {

                QuantizeSettings settings = new QuantizeSettings();
                settings.Colors = 256;
                settings.DitherMethod = DitherMethod.No;
                this.fridgeGifEncoder.Quantize(settings);

                // Optionally optimize the images (images should have the same size).
                this.fridgeGifEncoder.Optimize();

                // Save gif
                this.fridgeGifEncoder.Write(fridgeGifPath);
            }

            BitmapEncoder encoder = new PngBitmapEncoder();

            // create frame from the writable bitmap and add to encoder
            encoder.Frames.Add(BitmapFrame.Create(this.bitmap));

            string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);

            string myPhotos = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

            // write the new file to disk

            // FileStream is IDisposable
            using (FileStream fs = new FileStream("C:\\Dev\\Test-Fridge.png", FileMode.Create))
            {
                encoder.Save(fs);
            }
        }

        /// <summary>
        /// Handles the depth/color/body index frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            if (this.currentlyProcessingFrame)
                return;

            this.currentlyProcessingFrame = true;
            ++frame;
            int depthWidth = 0;
            int depthHeight = 0;

            bool multiSourceFrameProcessed = false;
            bool colorFrameProcessed = false;
            bool depthFrameProcessed = false;

            MultiSourceFrame multiSourceFrame = e.FrameReference.AcquireFrame();

            if (multiSourceFrame != null)
            {
                // Frame Acquisition should always occur first when using multiSourceFrameReader
                using (DepthFrame depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame())
                {
                    using (ColorFrame colorFrame = multiSourceFrame.ColorFrameReference.AcquireFrame())
                    {
                        if (depthFrame != null)
                        {
                            FrameDescription depthFrameDescription = depthFrame.FrameDescription;
                            depthWidth = depthFrameDescription.Width;
                            depthHeight = depthFrameDescription.Height;

                            if ((depthWidth * depthHeight) == this.depthFrameData.Length)
                            {
                                depthFrame.CopyFrameDataToArray(this.depthFrameData);

                                depthFrameProcessed = true;
                            }
                        }

                        if (colorFrame != null)
                        {
                            FrameDescription colorFrameDescription = colorFrame.FrameDescription;
                            this.colorWidth = colorFrameDescription.Width;
                            this.colorHeight = colorFrameDescription.Height;

                            if ((this.colorWidth * this.colorHeight * this.bytesPerPixel) == this.colorFrameData.Length)
                            {
                                if (colorFrame.RawColorImageFormat == ColorImageFormat.Bgra)
                                {
                                    colorFrame.CopyRawFrameDataToArray(this.colorFrameData);
                                }
                                else
                                {
                                    colorFrame.CopyConvertedFrameDataToArray(this.colorFrameData, ColorImageFormat.Bgra);
                                }

                                colorFrameProcessed = true;
                            }
                        }

                        multiSourceFrameProcessed = true;
                    }
                }
            }

            // we got all frames
            if (multiSourceFrameProcessed && depthFrameProcessed && colorFrameProcessed)
            {
                int endX = this.colorWidth - widthEndReduction;
                int endY = this.colorHeight - heightEndReduction;

                this.coordinateMapper.MapColorFrameToDepthSpace(this.depthFrameData, this.depthPoints);

                byte[] tempColorData = new byte[reducedWidth * reducedHeight * bytesPerPixel];
                ulong[] tempDepthData = new ulong[reducedWidth * reducedHeight];
                for (int y = startY; y < endY; y++)
                {
                    Array.Copy(this.colorFrameData, (y * this.colorWidth + startX) * bytesPerPixel, tempColorData, ((y - startY) * reducedWidth) * bytesPerPixel, reducedWidth * bytesPerPixel);
                }

                int depthIndex = 0;
                int adjustedDepthIndex = 0;
                for (int y = startY; y < endY; ++y)
                {
                    for (int x = startX; x < endX; ++x)
                    {
                        // calculate index into depth array
                        depthIndex = ((y * this.colorWidth) + x);
                        // retrieve the depth to color mapping for the current depth pixel

                        ushort result = PixelDepth(depthIndex, depthWidth);
                        tempDepthData[adjustedDepthIndex] = result;
                        adjustedDepthIndex++;
                    }
                }

                if (this.averagingDepthPixels)
                {
                    if (averageDepthFrame == null)
                    {
                        averageDepthFrame = new AverageDepthFrame(tempColorData, tempDepthData, minDepthOutlier, maxDepthOutlier);
                    }
                    else
                    {
                        averageDepthFrame.AddFrameToAverage(tempColorData, tempDepthData);
                    }
                    Bitmap depthBitmap = averageDepthFrame.GetComparisonDepthBitmap(this.averageDepthComparison);
                    this.bitmap.
                    //Array.Copy(tempColorData, this.displayPixels, tempColorData.Length);
                    this.RenderColorPixels();
                }
                else
                {
                    Frame addedFrame = frameHistory.AddFrame(tempColorData, tempDepthData);
                    if (addedFrame != null)
                    {
                        /*
                        addedFrame.GetComparisonDepthBitmap(
                        if (addedFrame. > currentMaxBackgroundPixels)
                        {
                            
                            Array.Copy(tempColorData, this.displayPixels, tempColorData.Length);
                            this.RenderColorPixels();
                        }
                         */
                    }
                }
                
            }
            this.currentlyProcessingFrame = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort PixelDepth(int depthIndex, int depthWidth)
        {
            if ((depthIndex < 0) || (depthIndex >= this.depthPoints.Length))
                return 0;

            DepthSpacePoint depthPoint = this.depthPoints[depthIndex];

            // make sure the depth pixel maps to a valid point in color space
            //int depthX = (int)Math.Floor(depthPoint.X);
            if (depthPoint.X > -1)
            {
                return this.depthFrameData[(int)depthPoint.Y * depthWidth + (int)depthPoint.X];
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Renders color pixels into the writeableBitmap.
        /// </summary>
        private void RenderColorPixels()
        {
            this.bitmap.WritePixels(
                new Int32Rect(0, 0, this.bitmap.PixelWidth, this.bitmap.PixelHeight),
                this.displayPixels,
                this.bitmap.PixelWidth * this.bytesPerPixel,
                0);
            /*
            if (this.iterationsSaved % 10000 == 0)
            {
                System.Drawing.Bitmap bmp = null;
                MemoryStream outStream = new MemoryStream();
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create((BitmapSource)this.bitmap));
                enc.Save(outStream);
                bmp = new System.Drawing.Bitmap(outStream);

                MagickImage newImage = new MagickImage(bmp);

                this.fridgeGifEncoder.Add(newImage);
            }
            */
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }

        private void TextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Array.Clear(this.displayPixels, 0, this.displayPixels.Length);
        }

        private void SaveAverageDepthPoints(object sender, RoutedEventArgs e)
        {
            this.averageDepthFrame.SaveClosedDepthPoints(this.closedDepthPath);
        }
    }
}
