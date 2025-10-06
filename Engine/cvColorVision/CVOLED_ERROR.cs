namespace cvColorVision
{
    public enum CVOLED_ERROR
    {
        CVOLED_SUCCESS = 0,
        CVOLED_PARAM_E,   //参数错误
        CVOLED_INPUT_E,   //输入错误
        CVOLED_SCRREN_NOT_SUPPORT, //屏幕类型不支持 
        CVOLED_INIT_E,     //初始化错误
        SAVE_E,           //保存文件错误
        OUT_OF_BOUNDRY,   //越界
        ALGORITHM_E,      //算法错误
        MORIE_E, //       摩尔纹
    };
}