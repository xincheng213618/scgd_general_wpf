using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Engine.CalFile
{
    public class CvCameraInfoModel
    {
        // --- 基础相机信息 ---
        [JsonProperty("camera_id")]
        public string CameraId { get; set; }

        [JsonProperty("camera_model")]
        public string CameraModel { get; set; }

        [JsonProperty("camera_sn")]
        public string CameraSn { get; set; }

        [JsonProperty("camera_type")]
        public string CameraType { get; set; }

        [JsonProperty("camera_interface_type")]
        public string CameraInterfaceType { get; set; }

        [JsonProperty("camera_illumination_type")]
        public string CameraIlluminationType { get; set; }

        [JsonProperty("camera_output_data_type")]
        public string CameraOutputDataType { get; set; }

        [JsonProperty("camera_output_image_depth")]
        public string CameraOutputImageDepth { get; set; }

        [JsonProperty("camera_save_image_depth")]
        public string CameraSaveImageDepth { get; set; }

        [JsonProperty("license_file")]
        public string LicenseFile { get; set; }

        // --- 相机参数与尺寸 ---
        [JsonProperty("camera_exposure")]
        public double CameraExposure { get; set; }

        [JsonProperty("camera_max_exposure_time")]
        public double CameraMaxExposureTime { get; set; }

        [JsonProperty("camera_width")]
        public int CameraWidth { get; set; }

        [JsonProperty("camera_height")]
        public int CameraHeight { get; set; }

        [JsonProperty("camera_temperature")]
        public double CameraTemperature { get; set; }

        // --- ROI (感兴趣区域) ---
        [JsonProperty("camera_roi_x")]
        public int CameraRoiX { get; set; }

        [JsonProperty("camera_roi_y")]
        public int CameraRoiY { get; set; }

        [JsonProperty("camera_roi_w")]
        public int CameraRoiW { get; set; }

        [JsonProperty("camera_roi_h")]
        public int CameraRoiH { get; set; }

        // --- 滤光轮 (Filter Wheel) ---
        [JsonProperty("filter_wheel_present")]
        public int FilterWheelPresent { get; set; } // 0 或 1

        [JsonProperty("filter_wheel_count")]
        public int FilterWheelCount { get; set; }

        [JsonProperty("filter_wheel_baud")]
        public int FilterWheelBaud { get; set; }

        [JsonProperty("filter_wheel_serial_control")]
        public int FilterWheelSerialControl { get; set; }

        // 滤光轮位置详细信息 (直接平铺映射)
        [JsonProperty("filter_wheel_pos_1_0")]
        public string FilterWheelPos1_0 { get; set; }
        [JsonProperty("filter_wheel_pos_1_1")]
        public string FilterWheelPos1_1 { get; set; }
        [JsonProperty("filter_wheel_pos_1_2")]
        public string FilterWheelPos1_2 { get; set; }
        [JsonProperty("filter_wheel_pos_1_3")]
        public string FilterWheelPos1_3 { get; set; }
        [JsonProperty("filter_wheel_pos_1_4")]
        public string FilterWheelPos1_4 { get; set; }
        [JsonProperty("filter_wheel_pos_1_5")]
        public string FilterWheelPos1_5 { get; set; }
        [JsonProperty("filter_wheel_pos_1_6")]
        public string FilterWheelPos1_6 { get; set; }

        [JsonProperty("filter_wheel_pos_2_5")]
        public string FilterWheelPos2_5 { get; set; }
        [JsonProperty("filter_wheel_pos_2_6")]
        public string FilterWheelPos2_6 { get; set; }
        [JsonProperty("filter_wheel_pos_2_7")]
        public string FilterWheelPos2_7 { get; set; }
        [JsonProperty("filter_wheel_pos_2_8")]
        public string FilterWheelPos2_8 { get; set; }
        [JsonProperty("filter_wheel_pos_2_9")]
        public string FilterWheelPos2_9 { get; set; }

        // --- 马达 (Motor) ---
        [JsonProperty("motor_present")]
        public int MotorPresent { get; set; }

        [JsonProperty("motor_type")]
        public string MotorType { get; set; }

        [JsonProperty("motor_baud")]
        public int MotorBaud { get; set; }

        [JsonProperty("motor_timeout")]
        public int MotorTimeout { get; set; }

        [JsonProperty("motor_query_method")]
        public string MotorQueryMethod { get; set; }

        [JsonProperty("motor_homing_method")]
        public string MotorHomingMethod { get; set; }

        [JsonProperty("motor_homing_timeout")]
        public int MotorHomingTimeout { get; set; }

        [JsonProperty("motor_focus_image_count")]
        public int MotorFocusImageCount { get; set; }

        // 马达速度与加速度参数
        [JsonProperty("motor_constant_speed")]
        public double MotorConstantSpeed { get; set; }

        [JsonProperty("motor_deceleration")]
        public double MotorDeceleration { get; set; }

        [JsonProperty("motor_high_speed_homing")]
        public int MotorHighSpeedHoming { get; set; } // 可能是速度值或开关

        [JsonProperty("motor_homing_acceleration")]
        public double MotorHomingAcceleration { get; set; }

        [JsonProperty("motor_low_speed_homing")]
        public int MotorLowSpeedHoming { get; set; }

        [JsonProperty("motor_run_acceleration")]
        public double MotorRunAcceleration { get; set; }

        [JsonProperty("motor_move_range_max")]
        public double MotorMoveRangeMax { get; set; }

        [JsonProperty("motor_move_range_min")]
        public double MotorMoveRangeMin { get; set; }

        // --- 快门 (Shutter) ---
        [JsonProperty("shutter_motorized")]
        public int ShutterMotorized { get; set; }

        [JsonProperty("shutter_baud")]
        public int ShutterBaud { get; set; }

        [JsonProperty("shutter_measure_delay")]
        public int ShutterMeasureDelay { get; set; }

        [JsonProperty("shutter_open_command")]
        public string ShutterOpenCommand { get; set; }

        [JsonProperty("shutter_close_command")]
        public string ShutterCloseCommand { get; set; }
    }
}
