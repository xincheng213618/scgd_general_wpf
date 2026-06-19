#pragma warning disable
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace cvColorVision
{
    public partial class cvCameraCSLib
    {
        public class CameraDiscoverySummary
        {
            public TimeSpan Elapsed { get; set; }
            public List<CameraDiscoveryModelResult> Models { get; set; } = new List<CameraDiscoveryModelResult>();
            public List<CameraDiscoveryItem> Cameras { get; set; } = new List<CameraDiscoveryItem>();
        }

        public class CameraDiscoveryModelResult
        {
            public CameraModel CameraModel { get; set; }
            public bool Success { get; set; }
            public TimeSpan Elapsed { get; set; }
            public int CameraCount { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
        }

        public class CameraDiscoveryItem
        {
            public CameraModel CameraModel { get; set; }
            public string CameraId { get; set; } = string.Empty;
            public string MD5Id { get; set; } = string.Empty;
            public TimeSpan SearchElapsed { get; set; }
        }

        public static IReadOnlyList<CameraModel> DefaultFastSearchCameraModels { get; } = new[]
        {
            CameraModel.QHY_USB,
            CameraModel.HK_USB,
            CameraModel.HK_CARD,
            CameraModel.HK_FG_CARD
        };

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetChannels",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetChannels(IntPtr handle, ref uint nChl);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetCameraID",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void CM_SetCameraID(IntPtr handle, string szCameraId);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetErrorMessage",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetErrorMessage(int nErr, StringBuilder szMsg, ref int len);

        public static bool CM_GetErrorMessage(int nErr, ref string szMsg)
        {
            StringBuilder builder = new StringBuilder(1024);
            int nLen = 1024;

            if (CM_GetErrorMessage(nErr, builder, ref nLen))
            {
                szMsg = builder.ToString();
                return true;
            }

            return false;
        }


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Reset",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_Reset(IntPtr handle);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "InitResource",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void InitResource(IntPtr CallBackFunc, IntPtr hOperate_data);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "ReleaseResource", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void ReleaseResource();
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CreatCameraManagerV1", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern IntPtr CM_CreatCameraManagerV1(CameraModel eMdl, CameraMode eMode, string cfgFilename);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetAllCameraID", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int GetAllCameraID(StringBuilder sn, int len);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetCameraIDV1",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern int GetAllCameraIDV1(CameraModel eMdl, StringBuilder sn, int len);
      
        public static bool GetAllCameraIDV1(CameraModel eMdl, ref string szText)
        {
            StringBuilder builder = new StringBuilder(1024);

            if (GetAllCameraIDV1(eMdl, builder, 1024) == cvErrorDefine.CV_ERR_SUCCESS)
            {
                szText = builder.ToString();
                return true;
            }

            return false;
        }

        public static CameraDiscoverySummary SearchCameraIds(IEnumerable<CameraModel> cameraModels, int bufferSize = 10240)
        {
            if (cameraModels == null)
            {
                throw new ArgumentNullException(nameof(cameraModels));
            }

            var summary = new CameraDiscoverySummary();
            var totalStopwatch = Stopwatch.StartNew();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (CameraModel cameraModel in cameraModels.Distinct())
            {
                var modelResult = new CameraDiscoveryModelResult { CameraModel = cameraModel };
                var modelStopwatch = Stopwatch.StartNew();

                try
                {
                    StringBuilder builder = new StringBuilder(bufferSize);
                    int ret = GetAllCameraIDV1(cameraModel, builder, bufferSize);
                    modelResult.Success = ret == cvErrorDefine.CV_ERR_SUCCESS;

                    if (modelResult.Success)
                    {
                        foreach (string cameraId in ExtractCameraIds(builder.ToString()))
                        {
                            string key = $"{cameraModel}:{cameraId}";
                            if (!seenKeys.Add(key))
                            {
                                continue;
                            }

                            summary.Cameras.Add(new CameraDiscoveryItem
                            {
                                CameraModel = cameraModel,
                                CameraId = cameraId,
                                MD5Id = GetMD5(cameraId).ToUpperInvariant(),
                                SearchElapsed = modelStopwatch.Elapsed
                            });
                        }
                    }
                    else
                    {
                        modelResult.ErrorMessage = $"CM_GetCameraIDV1 returned {ret}";
                    }
                }
                catch (Exception ex)
                {
                    modelResult.Success = false;
                    modelResult.ErrorMessage = ex.Message;
                }
                finally
                {
                    modelStopwatch.Stop();
                    modelResult.Elapsed = modelStopwatch.Elapsed;
                    modelResult.CameraCount = summary.Cameras.Count(a => a.CameraModel == cameraModel);
                    summary.Models.Add(modelResult);
                }
            }

            totalStopwatch.Stop();
            summary.Elapsed = totalStopwatch.Elapsed;
            return summary;
        }

        private static IEnumerable<string> ExtractCameraIds(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                yield break;
            }

            JObject jObject = JsonConvert.DeserializeObject<JObject>(json);
            JToken idToken = jObject?["ID"];
            if (idToken == null)
            {
                yield break;
            }

            foreach (JToken token in idToken.Children())
            {
                string cameraId = token.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(cameraId))
                {
                    yield return cameraId;
                }
            }
        }

        private static string GetMD5(string value)
        {
            using( MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(value));
                StringBuilder builder = new StringBuilder(32);
                foreach (byte item in hash)
                {
                    builder.Append(item.ToString("x2"));
                }
                return builder.ToString();
            }

        }
    }
}
