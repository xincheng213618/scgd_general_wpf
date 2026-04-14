using ST.Library.UI.NodeEditor;
using System.Text;

namespace ColorVision.UI.Tests
{
    public class STNodeCopyPasteTests
    {
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

            // Parse the save data format: [type_len][module|fullname][guid_len][guid][key-value pairs...]
            int pos = 0;
            string modelKey = Encoding.UTF8.GetString(data, pos + 1, data[pos]);
            pos += data[pos] + 1;
            string guidKey = Encoding.UTF8.GetString(data, pos + 1, data[pos]);
            pos += data[pos] + 1;

            // modelKey should be "module|FullTypeName"
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
        public void GetSaveData_DifferentNodeTypes_ProduceDifferentModuleKeys()
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

        [Fact]
        public void STNodeEditor_GetSelectedNode_InitiallyEmpty()
        {
            var editor = new STNodeEditor();
            var selected = editor.GetSelectedNode();
            Assert.NotNull(selected);
            Assert.Empty(selected);
        }
    }
}
