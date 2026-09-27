using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ColorVisionSetup
{
    internal static class PublisherSignature
    {
        // Public key of the existing ColorVision release signer. This is NOT a private key.
        // Trust is explicit key pinning, not the Windows root store or a matching CN string.
        // Renewals with the same key work; key rotation requires releasing a new helper.
        internal const string PublisherPublicKeySha256 = "E6C1B8412EAF1C4C7BE9BD2708F90BA9CF7CF49C9C5DEA407193076A1E09B4E9";
        private const uint Untrusted = 0x800B0004;
        private static readonly Guid GenericChainVerify = new Guid("fc451c16-ac75-11d1-b4b8-00c04fb66ea0");

        internal static void Verify(string path)
        {
            var policyCallback = new ChainPolicyCallback(CheckPublisher);
            var policy = new ChainPolicyData { Size = (uint)Marshal.SizeOf(typeof(ChainPolicyData)), Callback = policyCallback };
            var file = new TrustFile { Size = (uint)Marshal.SizeOf(typeof(TrustFile)), Path = path };
            IntPtr policyPointer = Marshal.AllocHGlobal(Marshal.SizeOf(policy));
            IntPtr filePointer = Marshal.AllocHGlobal(Marshal.SizeOf(file));
            var data = new TrustData
            {
                Size = (uint)Marshal.SizeOf(typeof(TrustData)), PolicyData = policyPointer,
                UIChoice = 2, UnionChoice = 1, File = filePointer, StateAction = 1,
                ProviderFlags = 0x1000 | 0x10 // Cache-only URL retrieval; offline key-based publisher policy.
            };
            try
            {
                Marshal.StructureToPtr(policy, policyPointer, false);
                Marshal.StructureToPtr(file, filePointer, false);
                Guid action = GenericChainVerify;
                uint result = WinVerifyTrust(new IntPtr(-1), ref action, ref data);
                if (result != 0) throw new InvalidDataException("安装包签名或发行公钥校验失败（0x" + result.ToString("X8") + "）。请重新下载官方安装包。");
            }
            finally
            {
                if (data.State != IntPtr.Zero)
                {
                    data.StateAction = 2;
                    Guid action = GenericChainVerify;
                    WinVerifyTrust(new IntPtr(-1), ref action, ref data);
                }
                Marshal.DestroyStructure(filePointer, typeof(TrustFile));
                Marshal.FreeHGlobal(filePointer);
                Marshal.FreeHGlobal(policyPointer);
                GC.KeepAlive(policyCallback);
            }
        }

        private static uint CheckPublisher(IntPtr provider, uint stepError, uint registryPolicy, uint signerCount, IntPtr signers, IntPtr argument)
        {
            // WinTrust verifies the PE digest and cryptographic signature before this final policy.
            // Never replace a signature/digest failure with our application trust decision.
            if (stepError != 0) return stepError;
            if (signerCount != 1 || signers == IntPtr.Zero) return Untrusted;
            try
            {
                var signer = (ChainSigner)Marshal.PtrToStructure(Marshal.ReadIntPtr(signers), typeof(ChainSigner));
                if (signer.Error != 0 || signer.Chain == IntPtr.Zero) return Untrusted;
                var chain = (ChainContext)Marshal.PtrToStructure(signer.Chain, typeof(ChainContext));
                if (chain.Count == 0 || chain.Chains == IntPtr.Zero) return Untrusted;
                var simple = (SimpleChain)Marshal.PtrToStructure(Marshal.ReadIntPtr(chain.Chains), typeof(SimpleChain));
                if (simple.Count == 0 || simple.Elements == IntPtr.Zero) return Untrusted;
                var element = (ChainElement)Marshal.PtrToStructure(Marshal.ReadIntPtr(simple.Elements), typeof(ChainElement));
                using (var certificate = new X509Certificate2(element.Certificate))
                using (var sha = SHA256.Create())
                {
                    string key = BitConverter.ToString(sha.ComputeHash(certificate.GetPublicKey())).Replace("-", "");
                    return string.Equals(key, PublisherPublicKeySha256, StringComparison.Ordinal) ? 0 : Untrusted;
                }
            }
            catch { return Untrusted; } // Exceptions must never cross the unmanaged callback boundary.
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate uint ChainPolicyCallback(IntPtr provider, uint stepError, uint registryPolicy, uint signerCount, IntPtr signers, IntPtr argument);
        [StructLayout(LayoutKind.Sequential)]
        private struct ChainPolicyData { public uint Size; public IntPtr SignerInfo, CounterSignerInfo; public ChainPolicyCallback Callback; public IntPtr Argument; }
        [StructLayout(LayoutKind.Sequential)]
        private struct ChainSigner { public uint Size; public IntPtr Chain; public uint SignerType; public IntPtr MessageSigner; public uint Error, CounterSignerCount; public IntPtr CounterSigners; }
        [StructLayout(LayoutKind.Sequential)]
        private struct ChainContext { public uint Size, ErrorStatus, InfoStatus, Count; public IntPtr Chains; }
        [StructLayout(LayoutKind.Sequential)]
        private struct SimpleChain { public uint Size, ErrorStatus, InfoStatus, Count; public IntPtr Elements; }
        [StructLayout(LayoutKind.Sequential)]
        private struct ChainElement { public uint Size; public IntPtr Certificate; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TrustFile { public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public IntPtr Handle, KnownSubject; }
        [StructLayout(LayoutKind.Sequential)]
        private struct TrustData
        {
            public uint Size;
            public IntPtr PolicyData, SipData;
            public uint UIChoice, RevocationChecks, UnionChoice;
            public IntPtr File;
            public uint StateAction;
            public IntPtr State, UrlReference;
            public uint ProviderFlags, UIContext;
            public IntPtr SignatureSettings;
        }
        [DllImport("wintrust.dll", ExactSpelling = true, PreserveSig = true)]
        private static extern uint WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
    }
}
