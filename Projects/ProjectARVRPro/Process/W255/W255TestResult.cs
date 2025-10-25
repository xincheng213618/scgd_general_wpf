using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp; // PoiPointResultModel

namespace ProjectARVRPro.Process.W255
{
    public class W255ViewTestResult : W255TestResult
    {
        public List<PoiResultCIExyuvData> PoixyuvDatas { get; set; } = new List<PoiResultCIExyuvData>();
    }

    public class W255TestResult : ViewModelBase
    {
        /// <summary>
        /// ˮƽ�ӳ���(��) ������
        /// </summary>
        public ObjectiveTestItem HorizontalFieldOfViewAngle { get; set; } = new ObjectiveTestItem() { Name = "Horizontal_Field_Of_View_Angle" ,Unit = "degree" };

        /// <summary>
        /// ��ֱ�ӳ���(��) ������
        /// </summary>
        public ObjectiveTestItem VerticalFieldOfViewAngle { get; set; } = new ObjectiveTestItem() { Name = "Vertical_Field of_View_Angle", Unit = "degree" };
        /// <summary>
        /// �Խ����ӳ���(��) ������
        /// </summary>
        public ObjectiveTestItem DiagonalFieldOfViewAngle { get; set; } = new ObjectiveTestItem() { Name = "Diagonal_Field_of_View_Angle", Unit = "degree" };

        /// <summary>
        /// ���Ⱦ�����(%) ������
        /// </summary>
        public ObjectiveTestItem LuminanceUniformity { get; set; } = new ObjectiveTestItem() { Name = "Luminance_Uniformity(min/max*100%)" };

        /// <summary>
        /// ɫ�ʾ����� ������
        /// </summary>
        public ObjectiveTestItem ColorUniformity { get; set; } = new ObjectiveTestItem() { Name = "Color_Uniformity(��u'v'max)" };

        /// <summary>
        /// ���ĵ�����
        /// </summary>
        public ObjectiveTestItem CenterLunimance { get; set; } = new ObjectiveTestItem() { Name = "Center_Lunimance ", Unit = "cd/m^2" };
        /// <summary>
        /// CenterCIE1931ChromaticCoordinatesx
        /// </summary>
        public ObjectiveTestItem CenterCIE1931ChromaticCoordinatesx { get; set; } = new ObjectiveTestItem() { Name = "Center_CIE_1931Chromatic_Coordinates_x" };
        /// <summary>
        /// CenterCIE1931ChromaticCoordinatesy
        /// </summary>
        public ObjectiveTestItem CenterCIE1931ChromaticCoordinatesy { get; set; } = new ObjectiveTestItem() { Name = "Center_CIE_1931Chromatic_Coordinates_y" };
        /// <summary>
        /// CenterCIE1976ChromaticCoordinatesu
        /// </summary>
        public ObjectiveTestItem CenterCIE1976ChromaticCoordinatesu { get; set; } = new ObjectiveTestItem() { Name = "Center_CIE_1931Chromatic_Coordinates_u'" };
        /// <summary>
        /// CenterCIE1976ChromaticCoordinatesv
        /// </summary>
        public ObjectiveTestItem CenterCIE1976ChromaticCoordinatesv { get; set; } = new ObjectiveTestItem() { Name = "Center_CIE_1931Chromatic_Coordinates_v'" };
    }
}
