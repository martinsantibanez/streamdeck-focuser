using BarRaider.SdTools;
using FontAwesome.Sharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Synkrono.FocusWindow.Backend;
using Synkrono.FocusWindow.Wrappers;

namespace Synkrono.FocusWindow.Actions
{
    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: Icessassin
    // Subscriber: Th33cho
    // Subscriber: imthefrog
    // 10001 Bits: nubby_ninja
    // 65 Bits: Beej324
    // Icessassin - Tip: 15.55
    //---------------------------------------------------
    //          Mentions:
    // Marbles On Stream Winner: Krinkelschmidt
    // Marbles On Stream Winner: AkeDalmans
    //---------------------------------------------------
    [PluginActionId("com.valpro.windowfocuser.displayaction")]
    class DisplayAction : PluginBase
    {
        #region Private Members

        private const int ICON_SIZE_PIXELS = 64;

        private readonly int deviceColumns = 0;
        private readonly int locationRow = 0;
        private readonly int locationColumn = 0;
        private readonly int sequentialKey;
        private readonly KeyCoordinates coordinates;
        private readonly SemaphoreSlim actionLock = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim imageCloneLock = new SemaphoreSlim(1, 1);

        #endregion

        public DisplayAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            var deviceInfo = payload.DeviceInfo.Devices.Where(d => d.Id == connection.DeviceId).FirstOrDefault();

            sequentialKey = 0;
            if (deviceInfo != null && payload?.Coordinates != null)
            {
                coordinates = payload.Coordinates;
                deviceColumns = deviceInfo.Size.Cols;
                locationRow = coordinates.Row;
                locationColumn = coordinates.Column;
                sequentialKey = (deviceColumns * locationRow) + locationColumn;
            }
            else
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"DisplayAction invalid ctor settings Device: {deviceInfo} Payload: {payload}");
            }
            UIManager.Instance.UIActionEvent += Instance_UIActionEvent;
        }

        #region Public Methods
        public override void Dispose()
        {
            UIManager.Instance.UIActionEvent -= Instance_UIActionEvent;
        }

        public override void KeyPressed(KeyPayload payload)
        {
            UIManager.Instance.NotifyKeyPressed(coordinates, false);
        }

        public override void KeyReleased(KeyPayload payload) {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"payload {payload.UserDesiredState} {payload.Settings}");
        }

        public override void OnTick() { }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        public override void ReceivedSettings(ReceivedSettingsPayload payload) { }

        #endregion

        #region Private Methods

        private async void Instance_UIActionEvent(object sender, Wrappers.UIActionEventArgs e)
        {
            try
            {
                UIActionSettings action = null;
                if (e.AllKeysAction) // If event is marked for "All Keys", get the first one
                {
                    action = e.Settings[0];
                }
                else
                {
                    // Try and find an action in the list that is for this specific coordinates
                    action = e.Settings.Where(actn => actn.Coordinates.IsCoordinatesSame(coordinates)).FirstOrDefault();
                }

                if (action != null)
                {
                    await HandleActionRequest(action);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"UIActionEvent Exception: {ex}");
            }
        }

        private async Task HandleActionRequest(UIActionSettings actionRequest)
        {
            await actionLock.WaitAsync();
            try
            {
                switch (actionRequest.Action)
                {
                    case UIActions.DrawTitle:
                        await Connection.SetImageAsync((string)null);
                        await Connection.SetTitleAsync(actionRequest.Title);
                        break;
                    case UIActions.DrawImage:
                        await Connection.SetTitleAsync((string)null);
                        await DrawImage(actionRequest);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"DisplayAction HandleActionRequest Exception: {ex}");
            }
            finally
            {
                actionLock.Release();
            }
        }

        private async Task DrawImage(UIActionSettings actionRequest)
        {
            //Logger.Instance.LogMessage(TracingLevel.INFO, $"Row {coordinates.Row} Column {coordinates.Column} Color {(actionRequest.BackgroundColor.HasValue ? actionRequest.BackgroundColor.Value.ToString() : "")} Title {actionRequest.Title ?? ""}");
            using (var img = await CreateImage(actionRequest))
            {
                await Connection.SetImageAsync(img);
            }
        }

        private async Task<Bitmap> CreateImage(UIActionSettings actionRequest)
        {
            Bitmap img = Tools.GenerateGenericKeyImage(out Graphics graphics);
            if (img == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"DrawImage failed, GenerateGenericKeyImage returned null");
                return null;
            }
            int height = img.Height;
            int width = img.Width;

            // If there is an image, draw it, otherwise, check for background
            if (actionRequest.Image != null)
            {
                var scaledImage = ScaleImage(await CloneImage(actionRequest.Image), new Size(width, height));
                if (scaledImage != null)
                {
                    graphics.DrawImage(scaledImage, new Point(0, 0));
                }
            }
            else
            {
                // Background
                var bgBrush = new SolidBrush(actionRequest.BackgroundColor ?? Color.Black);
                graphics.FillRectangle(bgBrush, 0, 0, width, height);
            }

            // If a FontAwesome image is requested, draw it in the center
            if (actionRequest.FontAwesomeIcon.HasValue)
            {
                Image icon = actionRequest.FontAwesomeIcon.Value.ToBitmap(Color.Red, ICON_SIZE_PIXELS);
                float iconWidth = (width - icon.Width) / 2;
                float iconHeight = (height - icon.Height) / 2;
                PointF iconStart = new PointF(iconWidth, iconHeight);
                graphics.DrawImage(icon, iconStart);
                icon.Dispose();
            }

            // Draw text title if needed
            if (!String.IsNullOrEmpty(actionRequest.Title))
            {
                using (var font = new Font("Verdana", 24, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    var fgBrush = Brushes.White;
                    string[] titleLines = actionRequest.Title.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Reverse().ToArray();

                    SizeF stringSize = graphics.MeasureString(titleLines[0], font);
                    float stringHeight = Math.Abs((height - stringSize.Height - 3));
                    foreach (string line in titleLines)
                    {
                        float textCenter = graphics.GetTextCenter(line, img.Width, font, out _);
                        float newPosition = graphics.DrawAndMeasureString(line, font, fgBrush, new PointF(textCenter, stringHeight));
                        stringHeight -= (newPosition - stringHeight);
                    }
                }
            }

            return img;
        }

        private async Task<Image> CloneImage(Image image)
        {
            await imageCloneLock.WaitAsync();
            try
            {
                return (Image)image.Clone();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"DisplayAction CloneImage Exception: {ex}");
            }

            finally
            {
                imageCloneLock.Release();
            }
            return null;
        }


        private Image ScaleImage(Image image, Size newSize)
        {
            if (image == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, "ScaleImage failed - Image is null");
                return null;
            }

            var newImage = new Bitmap(newSize.Width, newSize.Height, PixelFormat.Format24bppRgb);
            double scale = Math.Max((double)newSize.Width / image.Width, (double)newSize.Height / image.Height);
            using (var g = Graphics.FromImage(newImage))
            {
                g.InterpolationMode = InterpolationMode.High;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var scaleWidth = (int)(image.Width * scale);
                var scaleHeight = (int)(image.Height * scale);

                g.FillRectangle(Brushes.Black, new RectangleF(0, 0, newSize.Width, newSize.Height));
                g.DrawImage(image, new Rectangle(((int)newSize.Width - scaleWidth) / 2, ((int)newSize.Height - scaleHeight) / 2, scaleWidth, scaleHeight));
            }
            return newImage;
        }

        #endregion

    }
}
