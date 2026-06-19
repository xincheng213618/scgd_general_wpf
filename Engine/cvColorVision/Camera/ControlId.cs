#pragma warning disable

namespace cvColorVision
{
    public enum CONTROL_ID
    {
        /*0*/
        CONTROL_BRIGHTNESS = 0, //!< image brightness
        /*1*/
        CONTROL_CONTRAST,       //!< image contrast
        /*2*/
        CONTROL_WBR,            //!< red of white balance
        /*3*/
        CONTROL_WBB,            //!< blue of white balance
        /*4*/
        CONTROL_WBG,            //!< the green of white balance
        /*5*/
        CONTROL_GAMMA,          //!< screen gamma
        /*6*/
        CONTROL_GAIN,           //!< camera gain
        /*7*/
        CONTROL_OFFSET,         //!< camera offset
        /*8*/
        CONTROL_EXPOSURE,       //!< expose time (us)
        /*9*/
        CONTROL_SPEED,          //!< transfer speed
        /*10*/
        CONTROL_TRANSFERBIT,    //!< image depth bits
        /*11*/
        CONTROL_CHANNELS,       //!< image channels
        /*12*/
        CONTROL_USBTRAFFIC,     //!< hblank
        /*13*/
        CONTROL_ROWNOISERE,     //!< row denoise
        /*14*/
        CONTROL_CURTEMP,        //!< current cmos or ccd temprature
        /*15*/
        CONTROL_CURPWM,         //!< current cool pwm
        /*16*/
        CONTROL_MANULPWM,       //!< set the cool pwm
        /*17*/
        CONTROL_CFWPORT,        //!< control camera color filter wheel port
        /*18*/
        CONTROL_COOLER,         //!< check if camera has cooler
        /*19*/
        CONTROL_ST4PORT,        //!< check if camera has st4port
        /*20*/
        CAM_COLOR,
        /*21*/
        CAM_BIN1X1MODE,         //!< check if camera has bin1x1 mode
        /*22*/
        CAM_BIN2X2MODE,         //!< check if camera has bin2x2 mode
        /*23*/
        CAM_BIN3X3MODE,         //!< check if camera has bin3x3 mode
        /*24*/
        CAM_BIN4X4MODE,         //!< check if camera has bin4x4 mode
        /*25*/
        CAM_MECHANICALSHUTTER,                   //!< mechanical shutter
        /*26*/
        CAM_TRIGER_INTERFACE,                    //!< check if camera has triger interface
        /*27*/
        CAM_TECOVERPROTECT_INTERFACE,            //!< tec overprotect
        /*28*/
        CAM_SINGNALCLAMP_INTERFACE,              //!< singnal clamp
        /*29*/
        CAM_FINETONE_INTERFACE,                  //!< fine tone
        /*30*/
        CAM_SHUTTERMOTORHEATING_INTERFACE,       //!< shutter motor heating
        /*31*/
        CAM_CALIBRATEFPN_INTERFACE,              //!< calibrated frame
        /*32*/
        CAM_CHIPTEMPERATURESENSOR_INTERFACE,     //!< chip temperaure sensor
        /*33*/
        CAM_USBREADOUTSLOWEST_INTERFACE,         //!< usb readout slowest

        /*34*/
        CAM_8BITS,                               //!< 8bit depth
        /*35*/
        CAM_16BITS,                              //!< 16bit depth
        /*36*/
        CAM_GPS,                                 //!< check if camera has gps

        /*37*/
        CAM_IGNOREOVERSCAN_INTERFACE,            //!< ignore overscan area

        /*38*/
        QHYCCD_3A_AUTOBALANCE,
        /*39*/
        QHYCCD_3A_AUTOEXPOSURE,
        /*40*/
        QHYCCD_3A_AUTOFOCUS,
        /*41*/
        CONTROL_AMPV,                            //!< ccd or cmos ampv
        /*42*/
        CONTROL_VCAM,                            //!< Virtual Camera on off
        /*43*/
        CAM_VIEW_MODE,

        /*44*/
        CONTROL_CFWSLOTSNUM,         //!< check CFW slots number
        /*45*/
        IS_EXPOSING_DONE,
        /*46*/
        ScreenStretchB,
        /*47*/
        ScreenStretchW,
        /*48*/
        CONTROL_DDR,
        /*49*/
        CAM_LIGHT_PERFORMANCE_MODE,

        /*50*/
        CAM_QHY5II_GUIDE_MODE,
        /*51*/
        DDR_BUFFER_CAPACITY,
        /*52*/
        DDR_BUFFER_READ_THRESHOLD,
        /*53*/
        DefaultGain,
        /*54*/
        DefaultOffset,
        /*55*/
        OutputDataActualBits,
        /*56*/
        OutputDataAlignment,

        /*57*/
        CAM_SINGLEFRAMEMODE,
        /*58*/
        CAM_LIVEVIDEOMODE,
        /*59*/
        CAM_IS_COLOR,
        /*60*/
        hasHardwareFrameCounter,
        /*61*/
        CONTROL_MAX_ID_Error, //** No Use , last max index */
        /*62*/
        CAM_HUMIDITY,           //!<check if camera has	 humidity sensor  20191021 LYL Unified humidity function
        /*63*/
        CAM_PRESSURE,             //check if camera has pressure sensor
        /*64*/
        CONTROL_VACUUM_PUMP,        /// if camera has VACUUM PUMP
        /*65*/
        CONTROL_SensorChamberCycle_PUMP, ///air cycle pump for sensor drying
        /*66*/
        CAM_32BITS,
        /*67*/
        CAM_Sensor_ULVO_Status, /// Sensor working status [0:init  1:good  2:checkErr  3:monitorErr 8:good 9:powerChipErr]  410 461 411 600 268 [Eris board]
        /*68*/
        CAM_SensorPhaseReTrain, /// 2020,4040/PRO，6060,42PRO
        /*69*/
        CAM_InitConfigFromFlash, /// 2410 461 411 600 268 for now
        /*70*/
        CAM_TRIGER_MODE, //check if camera has multiple triger mode
        /*71*/
        CAM_TRIGER_OUT, //check if camera support triger out function
        /*72*/
        CAM_BURST_MODE, //check if camera support burst mode


        /* Do not Put Item after  CONTROL_MAX_ID !! This should be the max index of the list */
        /*Last One */
        CONTROL_MAX_ID
    };
}
