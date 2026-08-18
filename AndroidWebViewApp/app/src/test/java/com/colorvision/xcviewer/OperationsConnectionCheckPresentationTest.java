package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class OperationsConnectionCheckPresentationTest {
    @Test
    public void resultKeepsRecommendationSeparateFromTechnicalEvidence() {
        OperationsConnectionCheck.Result result = new OperationsConnectionCheck.Result(
                false,
                "电脑端安全端口没有响应",
                "请确认 ColorVision 正在运行。",
                "1. 手机网络：Wi-Fi\n2. 目标主机：192.168.1.2\n3. 主机解析：通过\n4. TCP 端口：连接超时");

        assertEquals(4, result.completedCheckCount);
        assertFalse(result.technicalDetails.contains("请确认"));
        assertEquals("请确认 ColorVision 正在运行。", result.recommendation);
    }

    @Test
    public void failureStatusLeadsWithRequiredAttention() {
        assertEquals(
                "需要处理 · 电脑端安全端口没有响应",
                OperationsConnectionCheckPresentation.status(
                        false, "电脑端安全端口没有响应"));
        assertEquals(
                "需要处理 · 连接检查未通过",
                OperationsConnectionCheckPresentation.status(false, " "));
    }

    @Test
    public void diagnosticDisclosureUsesCompletedCheckCount() {
        String summary = OperationsConnectionCheckPresentation.diagnosticSummary(4);

        assertTrue(summary.startsWith("已完成 4 项只读检查"));
        assertTrue(summary.contains("不包含设备密钥"));
        assertEquals(
                "查看 4 项检查详情",
                OperationsConnectionCheckPresentation.detailsAction(4, false));
        assertEquals(
                "收起检查详情",
                OperationsConnectionCheckPresentation.detailsAction(4, true));
    }

    @Test
    public void startupFailureDoesNotClaimThatChecksCompleted() {
        assertTrue(OperationsConnectionCheckPresentation.diagnosticSummary(0)
                .startsWith("自检未能开始"));
        assertEquals(
                "查看启动错误详情",
                OperationsConnectionCheckPresentation.detailsAction(0, false));
    }
}
