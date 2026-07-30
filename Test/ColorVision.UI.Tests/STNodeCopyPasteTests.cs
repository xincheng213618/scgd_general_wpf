#pragma warning disable CA1707
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ColorVision.UI.Tests
{
    public class STNodeCopyPasteTests
    {
        private const string RequiresStaUiTestRunner = "Requires an STA UI test runner.";

        [Fact]
        public void EmptyOption_ReportsZeroConnections()
        {
            Assert.Equal(0, STNodeOption.Empty.ConnectionCount);
            Assert.Empty(STNodeOption.Empty.ConnectedOption);
        }

        [Fact]
        public void Title_Set_RaisesPropertyChanged()
        {
            var node = new STNodeHub();
            string? propertyName = null;
            node.PropertyChanged += (_, e) => propertyName = e.PropertyName;

            node.Title = "Updated title";

            Assert.Equal("Updated title", node.Title);
            Assert.Equal(nameof(STNode.Title), propertyName);
        }

        [Fact]
        public void GetSaveData_RoundTrip_ParsesCorrectly()
        {
            var node = new STNodeHub();
            node.Create();
            node.Left = 100;
            node.Top = 200;

            byte[] data = node.GetSaveData();
            Assert.NotNull(data);
            Assert.True(data.Length > 0);

            // Parse the save data format: [type_len][module|full-name][guid_len][guid][key-value pairs...]
            int pos = 0;
            string modelKey = Encoding.UTF8.GetString(data, pos + 1, data[pos]);
            pos += data[pos] + 1;
            string guidKey = Encoding.UTF8.GetString(data, pos + 1, data[pos]);
            pos += data[pos] + 1;

            // Older readers require the persisted full type name.
            Assert.Contains("|", modelKey);
            string typeName = modelKey.Split('|')[1];
            Assert.Equal(typeof(STNodeHub).FullName, typeName);

            // guidKey should be a valid GUID
            Assert.True(Guid.TryParse(guidKey, out _));

            // Remaining bytes are key-value pairs
            var dic = new Dictionary<string, byte[]>();
            while (pos < data.Length)
            {
                int keyLen = BitConverter.ToInt32(data, pos); pos += 4;
                string key = Encoding.UTF8.GetString(data, pos, keyLen); pos += keyLen;
                int valLen = BitConverter.ToInt32(data, pos); pos += 4;
                byte[] val = new byte[valLen];
                Array.Copy(data, pos, val, 0, valLen); pos += valLen;
                dic[key] = val;
            }

            // Should have parsed to exactly the end
            Assert.Equal(data.Length, pos);
        }

        [Fact]
        public void GetSaveData_ContainsLocationData()
        {
            var node = new STNodeHub();
            node.Create();
            node.Left = 150;
            node.Top = 250;

            byte[] data = node.GetSaveData();
            Assert.NotNull(data);

            // Parse to dictionary
            int pos = 0;
            pos += data[pos] + 1; // skip modelKey
            pos += data[pos] + 1; // skip guidKey

            var dic = new Dictionary<string, byte[]>();
            while (pos < data.Length)
            {
                int keyLen = BitConverter.ToInt32(data, pos); pos += 4;
                string key = Encoding.UTF8.GetString(data, pos, keyLen); pos += keyLen;
                int valLen = BitConverter.ToInt32(data, pos); pos += 4;
                byte[] val = new byte[valLen];
                Array.Copy(data, pos, val, 0, valLen); pos += valLen;
                dic[key] = val;
            }

            // Node save data typically includes Left, Top
            Assert.True(dic.ContainsKey("Left"), "Save data should contain 'Left' key");
            Assert.True(dic.ContainsKey("Top"), "Save data should contain 'Top' key");

            int left = BitConverter.ToInt32(dic["Left"], 0);
            int top = BitConverter.ToInt32(dic["Top"], 0);
            Assert.Equal(150, left);
            Assert.Equal(250, top);
        }

        [Fact]
        public void GetAllInputOptions_AlwaysReturnsNonNull()
        {
            var node = new STNodeHub();
            node.Create();

            var inputs = node.GetAllInputOptions();
            Assert.NotNull(inputs);
        }

        [Fact]
        public void GetAllOutputOptions_AlwaysReturnsNonNull()
        {
            var node = new STNodeHub();
            node.Create();

            var outputs = node.GetAllOutputOptions();
            Assert.NotNull(outputs);
        }

        [Fact]
        public void GetAllOptions_ReturnOptionsEvenWhenGetOptionsReturnsNull()
        {
            // STNodeHub's LetGetOptions may be false, so GetInputOptions/GetOutputOptions can return null
            var node = new STNodeHub();
            node.Create();

            // GetAllInputOptions/GetAllOutputOptions should always work
            var allInputs = node.GetAllInputOptions();
            var allOutputs = node.GetAllOutputOptions();

            Assert.NotNull(allInputs);
            Assert.NotNull(allOutputs);

            // Hub nodes should have at least some options after Create()
            Assert.True(allInputs.Length > 0 || allOutputs.Length > 0,
                "Hub node should have at least one input or output option after Create()");
        }

        [Fact]
        public void GetSaveData_OnLoadNode_RoundTrip()
        {
            var original = new STNodeHub();
            original.Create();
            original.Left = 300;
            original.Top = 400;

            byte[] data = original.GetSaveData();

            // Parse save data into dictionary (same as CreateNodeFromSaveData does)
            int pos = 0;
            pos += data[pos] + 1; // skip modelKey
            pos += data[pos] + 1; // skip guidKey

            var dic = new Dictionary<string, byte[]>();
            while (pos < data.Length)
            {
                int keyLen = BitConverter.ToInt32(data, pos); pos += 4;
                string key = Encoding.UTF8.GetString(data, pos, keyLen); pos += keyLen;
                int valLen = BitConverter.ToInt32(data, pos); pos += 4;
                byte[] val = new byte[valLen];
                Array.Copy(data, pos, val, 0, valLen); pos += valLen;
                dic[key] = val;
            }

            // Create new node and load saved state
            var restored = new STNodeHub();
            restored.Create();
            restored.OnLoadNode(dic);

            Assert.Equal(300, restored.Left);
            Assert.Equal(400, restored.Top);
        }

        [Fact]
        public void GetSaveData_DifferentNodeTypes_ProduceDifferentModelKeys()
        {
            var hub = new STNodeHub();
            hub.Create();
            var inHub = new STNodeInHub();
            inHub.Create();

            byte[] hubData = hub.GetSaveData();
            byte[] inHubData = inHub.GetSaveData();

            // Parse module keys
            string hubKey = Encoding.UTF8.GetString(hubData, 1, hubData[0]);
            string inHubKey = Encoding.UTF8.GetString(inHubData, 1, inHubData[0]);

            // Same module, different type names
            string hubType = hubKey.Split('|')[1];
            string inHubType = inHubKey.Split('|')[1];

            Assert.NotEqual(hubType, inHubType);
            Assert.Equal(typeof(STNodeHub).FullName, hubType);
            Assert.Equal(typeof(STNodeInHub).FullName, inHubType);
        }

        [Fact]
        public void CanvasLoad_UsesModelKeyWhenTypeGuidDoesNotMatch()
        {
            var original = new STNodeHub();
            original.Create();
            byte[] nodeData = original.GetSaveData();

            int guidLengthOffset = nodeData[0] + 1;
            int guidLength = nodeData[guidLengthOffset];
            byte[] replacementGuid = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
            Assert.Equal(guidLength, replacementGuid.Length);
            Array.Copy(replacementGuid, 0, nodeData, guidLengthOffset + 1, guidLength);

            var container = new CVNodeContainer();
            Assert.True(container.LoadAssembly(typeof(STNodeHub).Assembly));
            container.LoadCanvas(CreateCanvasData(nodeData));

            Assert.IsType<STNodeHub>(Assert.Single(container.Nodes.Cast<STNode>()));
        }

        [Fact]
        public void NodeSaveDataKeepsCurrentModelKey()
        {
            var node = new STNodeHub();
            node.Create();

            byte[] nodeData = node.GetSaveData();
            string modelKey = Encoding.UTF8.GetString(nodeData, 1, nodeData[0]);

            Assert.Equal("ST.Library.UI.dll|ST.Library.UI.NodeEditor.STNodeHub", modelKey);
        }

        [Fact]
        public void CanvasLoad_ResolvesMovedFullTypeNameBySuffix()
        {
            var original = new STNodeHub();
            original.Create();
            byte[] nodeData = ReplaceNodeIdentityForReadTest(
                original.GetSaveData(),
                "ST.Library.UI.dll|Legacy.Library.Nodes.STNodeHub",
                Guid.NewGuid().ToString());

            var container = new CVNodeContainer();
            Assert.True(container.LoadAssembly(typeof(STNodeHub).Assembly));
            container.LoadCanvas(CreateCanvasData(nodeData));

            Assert.IsType<STNodeHub>(Assert.Single(container.Nodes.Cast<STNode>()));
        }

        [Fact]
        public void CanvasLoad_StillReadsShortModelKey()
        {
            var original = new STNodeHub();
            original.Create();
            byte[] nodeData = ReplaceNodeIdentityForReadTest(
                original.GetSaveData(),
                "ST.Library.UI.dll|STNodeHub",
                Guid.NewGuid().ToString());

            var container = new CVNodeContainer();
            Assert.True(container.LoadAssembly(typeof(STNodeHub).Assembly));
            container.LoadCanvas(CreateCanvasData(nodeData));

            Assert.IsType<STNodeHub>(Assert.Single(container.Nodes.Cast<STNode>()));
        }

        [Fact]
        public void CanvasLoad_ReadsStreamsThatReturnPartialChunks()
        {
            var original = new STNodeHub();
            original.Create();
            byte[] canvas = CreateCanvasData(original.GetSaveData());
            var container = new CVNodeContainer();
            Assert.True(container.LoadAssembly(typeof(STNodeHub).Assembly));
            using var stream = new ChunkedReadStream(canvas, maximumChunkSize: 1);

            container.LoadCanvas(stream);

            Assert.IsType<STNodeHub>(Assert.Single(container.Nodes.Cast<STNode>()));
        }

        [Fact]
        public void CanvasLoad_LateCorruptionDoesNotReplaceExistingContainerGraph()
        {
            var original = new STNodeHub();
            original.Create();
            var replacement = new STNodeInHub();
            replacement.Create();
            var container = new CVNodeContainer();
            Assert.True(container.LoadAssembly(typeof(STNodeHub).Assembly));
            container.LoadCanvas(CreateCanvasData(original.GetSaveData()));
            STNode existing = Assert.Single(container.Nodes.Cast<STNode>());
            byte[] replacementCanvas = CreateCanvasData(replacement.GetSaveData());
            Array.Resize(ref replacementCanvas, replacementCanvas.Length - 3);

            Assert.Throws<InvalidDataException>(() =>
                container.LoadCanvas(replacementCanvas));

            Assert.Single(container.Nodes.Cast<STNode>());
            Assert.Same(existing, container.Nodes[0]);
        }

        private static byte[] ReplaceNodeIdentityForReadTest(byte[] nodeData, string modelKey, string typeGuid)
        {
            int offset = 0;
            offset += nodeData[offset] + 1;
            offset += nodeData[offset] + 1;

            byte[] modelBytes = Encoding.UTF8.GetBytes(modelKey);
            byte[] guidBytes = Encoding.UTF8.GetBytes(typeGuid);
            Assert.True(modelBytes.Length <= byte.MaxValue);
            Assert.True(guidBytes.Length <= byte.MaxValue);

            using var stream = new MemoryStream();
            stream.WriteByte((byte)modelBytes.Length);
            stream.Write(modelBytes);
            stream.WriteByte((byte)guidBytes.Length);
            stream.Write(guidBytes);
            stream.Write(nodeData, offset, nodeData.Length - offset);
            return stream.ToArray();
        }

        private static byte[] CreateCanvasData(byte[] nodeData)
        {
            using var stream = new MemoryStream();
            stream.Write(STNodeConstant.NodeFlag);
            stream.WriteByte(STNodeConstant.Version);

            using (var gzip = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true))
            {
                gzip.Write(BitConverter.GetBytes(0f));
                gzip.Write(BitConverter.GetBytes(0f));
                gzip.Write(BitConverter.GetBytes(1f));
                gzip.Write(BitConverter.GetBytes(1));
                gzip.Write(BitConverter.GetBytes(nodeData.Length));
                gzip.Write(nodeData);
                gzip.Write(BitConverter.GetBytes(0));
            }

            return stream.ToArray();
        }

        private sealed class ChunkedReadStream : Stream
        {
            private readonly MemoryStream inner;
            private readonly int maximumChunkSize;

            public ChunkedReadStream(byte[] data, int maximumChunkSize)
            {
                inner = new MemoryStream(data, writable: false);
                this.maximumChunkSize = maximumChunkSize;
            }

            public override bool CanRead => true;
            public override bool CanSeek => inner.CanSeek;
            public override bool CanWrite => false;
            public override long Length => inner.Length;
            public override long Position
            {
                get => inner.Position;
                set => inner.Position = value;
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return inner.Read(
                    buffer,
                    offset,
                    Math.Min(count, maximumChunkSize));
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return inner.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    inner.Dispose();
                base.Dispose(disposing);
            }
        }

        [Fact(Skip = RequiresStaUiTestRunner)]
        public void STNodeEditor_AddAndRemoveNodes()
        {
            var editor = new STNodeEditor();
            var node1 = new STNodeHub();
            node1.Create();
            var node2 = new STNodeInHub();
            node2.Create();

            editor.Nodes.Add(node1);
            editor.Nodes.Add(node2);
            Assert.Equal(2, editor.Nodes.Count);

            editor.Nodes.Remove(node1);
            Assert.Equal(1, editor.Nodes.Count);
        }

        [Fact(Skip = RequiresStaUiTestRunner)]
        public void STNodeEditor_GetSelectedNode_InitiallyEmpty()
        {
            var editor = new STNodeEditor();
            var selected = editor.GetSelectedNode();
            Assert.NotNull(selected);
            Assert.Empty(selected);
        }
    }
}
