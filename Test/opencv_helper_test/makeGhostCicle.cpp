#include "function.h"


int makeGhostCicle(int width, int height, double smallRadius, double bigRadius, double backgroundColor[], double drawColor[],  const char* filePath, int flag)
{
    
    //������ɫ
    double B0 = backgroundColor[0];
    double G0 = backgroundColor[1];
    double R0 = backgroundColor[2];


    //��ͼ��ɫ
    double B1 = drawColor[0];
    double G1 = drawColor[1];
    double R1 = drawColor[2];
    
    
    // ��������ͼ��[1,3,5](@ref)
    Mat img(height, width, CV_8UC3, Scalar(B0, G0, R0));  // ��ͨ����ɫ����

    // ����ͼ����������[1,2,8](@ref)
    Point center(img.cols / 2, img.rows / 2);  // (960, 960)    
    int halfField = sqrt(pow(height / 2, 2) + pow(width / 2, 2));
    int outer_radius;
    int inner_radius;
    if (flag==0)
    {
        // ����Բ������
        outer_radius = bigRadius;   // ��Բ�뾶
        inner_radius = smallRadius;   // ��Բ�뾶 
    }
    if (flag==1)
    {
        // ����Բ������
        outer_radius =int(bigRadius* halfField);   // ��Բ�뾶
        inner_radius = int(smallRadius* halfField);   // ��Բ�뾶
    }
    Scalar white(B1, G1, R1);  // BGR��ʽ��ɫ[3,5](@ref)
   // ������Բ����ɫ��䣩[1,3](@ref)
    circle(img, center, outer_radius, white, -1, LINE_AA);
    // ������Բ����ɫ����γ��οգ�[2,3](@ref)
    circle(img, center, inner_radius, Scalar(B0, G0, R0), -1, LINE_AA);
    // ����ΪBMP��ʽ[6,7](@ref)
    imwrite(filePath, img);
    return 0;
}