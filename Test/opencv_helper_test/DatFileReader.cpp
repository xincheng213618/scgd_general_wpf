#include <opencv2/opencv.hpp>
#include <iostream>
#include <vector>
#include <string>

using namespace cv;
using namespace std;

// 1. 定义完全相同的结构体
struct PositionAttribute
{
    double step_x;
    double step_y;
    double angle;
    int rows;
    int cols;
    cv::Point2f center;
    cv::Rect outline;
    double mean_value;
    int type;   //0默认 1给定角点
    cv::Point2f cornet_pts[4];
    cv::Point2f shift_x;
    cv::Point2f shift_y;
};

// 2. 原始读取函数 (稍作修改以适应控制台输出)
int readPositionFileFromDat(std::string filename, cv::Mat& data, PositionAttribute& attri)
{
    Mat data_t;
    FILE* file;
    // 使用 fopen_s (Windows) 或 fopen (Linux/Mac)
#ifdef _WIN32
    errno_t err = fopen_s(&file, filename.c_str(), "rb");
    if (err != 0) {
        cerr << "无法打开文件: " << filename << endl;
        return -1;
    }
#else
    file = fopen(filename.c_str(), "rb");
    if (!file) {
        cerr << "无法打开文件: " << filename << endl;
        return -1;
    }
#endif

    uint32_t height = 0, width = 0;
    uint16_t type = CV_64FC2; // 对应 C# MatType.CV_64FC2
    uint16_t elemSize = 16;   // 8 bytes * 2 channels

    // 读取 Height
    if (fread(&height, 4, 1, file) != 1) {
        cerr << "读取 Height 失败" << endl; fclose(file); return -1;
    }
    // 读取 Width
    if (fread(&width, 4, 1, file) != 1) {
        cerr << "读取 Width 失败" << endl; fclose(file); return -1;
    }
    // 读取 Attribute
    if (fread(&attri, sizeof(attri), 1, file) != 1) {
        cerr << "读取 Attribute 失败" << endl; fclose(file); return -1;
    }

    // 读取 Mat Data
    uint64_t size = (uint64_t)height * width * elemSize;
    // 使用 char* 缓冲区
    char* buffer = new char[size];

    if (fread(buffer, 1, size, file) == size) {
        // 创建 Mat (注意：这里直接使用 buffer 数据，但在函数返回后 buffer 会被 delete，所以需要 clone)
        data_t = Mat(height, width, type, buffer);
        data = data_t.clone(); // 深拷贝数据

        cout << "文件加载成功: " << filename << endl;
        delete[] buffer;
        fclose(file);
        return 0;
    }
    else {
        cerr << "读取 Mat 数据体失败 (大小不匹配)" << endl;
        delete[] buffer;
        data = Mat(0, 0, type, Scalar(0));
        fclose(file);
        return 1;
    }
}

int main()
{
    // 修改为你的文件路径
    string filename = "C:\\Users\\Xin\\Desktop\\20260131T140254.0361975_FindLED_po.dat";

    Mat data;
    PositionAttribute attr;

    if (readPositionFileFromDat(filename, data, attr) == 0)
    {
        cout << "========================================" << endl;
        cout << "               C++ 读取验证              " << endl;
        cout << "========================================" << endl;

        // 1. 验证基础尺寸
        cout << "[Header Info]" << endl;
        cout << "Mat Size: " << data.cols << " x " << data.rows << endl;
        cout << "Channels: " << data.channels() << endl;
        cout << "ElemSize: " << data.elemSize() << " bytes" << endl;

        // 2. 验证 Attribute 数据
        cout << "\n[Attribute Info]" << endl;
        cout << "Angle: " << attr.angle << endl;
        cout << "Rows: " << attr.rows << ", Cols: " << attr.cols << endl;
        cout << "Center: (" << attr.center.x << ", " << attr.center.y << ")" << endl;
        cout << "Rect: [" << attr.outline.x << ", " << attr.outline.y << ", "
            << attr.outline.width << ", " << attr.outline.height << "]" << endl;

        cout << "Corner Points[0]: (" << attr.cornet_pts[0].x << ", " << attr.cornet_pts[0].y << ")" << endl;

        // 3. 验证 Mat 像素数据 (CV_64FC2 -> Vec2d)
        // 打印几个关键点的数据供对比
        cout << "\n[Mat Data Samples (Double, Double)]" << endl;

        // 左上角 (0,0)
        if (data.rows > 0 && data.cols > 0) {
            Vec2d p0 = data.at<Vec2d>(0, 0);
            cout << "Pixel(0,0): X=" << p0[0] << ", Y=" << p0[1] << endl;
        }

        // 中心点
        int cy = data.rows / 2;
        int cx = data.cols / 2;
        if (cy < data.rows && cx < data.cols) {
            Vec2d pc = data.at<Vec2d>(cy, cx);
            cout << "Pixel(" << cy << "," << cx << "): X=" << pc[0] << ", Y=" << pc[1] << endl;
        }

        // 最后一个点
        int ly = data.rows - 1;
        int lx = data.cols - 1;
        if (ly >= 0 && lx >= 0) {
            Vec2d pl = data.at<Vec2d>(ly, lx);
            cout << "Pixel(" << ly << "," << lx << "): X=" << pl[0] << ", Y=" << pl[1] << endl;
        }
    }
    else {
    }

    return 0;
}