package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsDashboardStatusFormatterTest {
    @Test
    public void attentionStatesCarrySemanticTone() {
        assertStatus("检测", "运行中", OperationsDashboardStatusFormatter.TONE_ACTIVE,
                OperationsDashboardStatusFormatter.flow(true, true, "running"));
        assertStatus("设备", "需关注 2", OperationsDashboardStatusFormatter.TONE_ATTENTION,
                OperationsDashboardStatusFormatter.devices(true, true, 3, 1, 2, 6));
        assertStatus("消息", "订阅 3 / 5", OperationsDashboardStatusFormatter.TONE_ATTENTION,
                OperationsDashboardStatusFormatter.messageChannel(true, true, false, 3, 5));
        assertStatus("告警", "严重 1", OperationsDashboardStatusFormatter.TONE_ATTENTION,
                OperationsDashboardStatusFormatter.alerts(true, 4, 2, 1));
        assertStatus("性能", "CPU 13% · 无响应",
                OperationsDashboardStatusFormatter.TONE_ATTENTION,
                OperationsDashboardStatusFormatter.performance(true, 12.6, "unresponsive"));
        assertStatus("恢复", "自动看门狗", OperationsDashboardStatusFormatter.TONE_DEFAULT,
                OperationsDashboardStatusFormatter.recovery(true, true, true, true));
    }

    @Test
    public void healthyStatesStayNeutralAndShort() {
        assertStatus("应用", "1.4.12.56 · 窗口可见 · 472 MB",
                OperationsDashboardStatusFormatter.TONE_DEFAULT,
                OperationsDashboardStatusFormatter.application(
                        true, "1.4.12.56", true, true, "Normal", 471.8));
        assertStatus("检测", "空闲", OperationsDashboardStatusFormatter.TONE_DEFAULT,
                OperationsDashboardStatusFormatter.flow(true, false, "idle"));
        assertStatus("设备", "就绪 4 / 4", OperationsDashboardStatusFormatter.TONE_DEFAULT,
                OperationsDashboardStatusFormatter.devices(true, true, 4, 0, 0, 4));
        assertStatus("消息", "已连接", OperationsDashboardStatusFormatter.TONE_DEFAULT,
                OperationsDashboardStatusFormatter.messageChannel(true, true, true, 5, 5));
        assertStatus("告警", "暂无异常", OperationsDashboardStatusFormatter.TONE_DEFAULT,
                OperationsDashboardStatusFormatter.alerts(true, 0, 0, 0));
        assertStatus("恢复", "Windows 后备", OperationsDashboardStatusFormatter.TONE_DEFAULT,
                OperationsDashboardStatusFormatter.recovery(true, true, true, false));
    }

    @Test
    public void missingUiResponsivenessIsNotReportedAsHealthy() {
        assertStatus("应用", "1.4.12.56 · 窗口不可用",
                OperationsDashboardStatusFormatter.TONE_ATTENTION,
                OperationsDashboardStatusFormatter.application(
                        true, "1.4.12.56", false, false, "", 0));
        assertStatus("应用", "暂不可用", OperationsDashboardStatusFormatter.TONE_MUTED,
                OperationsDashboardStatusFormatter.application(
                        false, "", false, false, "", 0));
        assertStatus("性能", "CPU 13% · 界面未知", OperationsDashboardStatusFormatter.TONE_MUTED,
                OperationsDashboardStatusFormatter.performance(true, 12.6, "unavailable"));
        assertStatus("告警", "暂不可用", OperationsDashboardStatusFormatter.TONE_MUTED,
                OperationsDashboardStatusFormatter.alerts(false, 0, 0, 0));
        assertStatus("恢复", "暂不可用", OperationsDashboardStatusFormatter.TONE_MUTED,
                OperationsDashboardStatusFormatter.recovery(false, false, false, false));
    }

    @Test
    public void signedSnapshotCopySeparatesFreshAndStaleEvidence() {
        assertEquals("实时状态",
                OperationsDashboardStatusFormatter.sectionTitle(false, true));
        assertEquals("电脑签名状态",
                OperationsDashboardStatusFormatter.sectionTitle(true, true));
        assertEquals("上次电脑签名状态",
                OperationsDashboardStatusFormatter.sectionTitle(true, false));
        assertTrue(OperationsDashboardStatusFormatter.sectionCaption(true, true)
                .contains("刚刚签名返回"));
        assertTrue(OperationsDashboardStatusFormatter.sectionCaption(true, false)
                .contains("已过期，仅供参考"));
        assertEquals("设备，需关注 2，查看详情",
                OperationsDashboardStatusFormatter.devices(true, true, 3, 1, 2, 6)
                        .accessibilityLabel());
    }

    @Test
    public void flowCancellationOnlyEnablesForExplicitlyCancellableActiveFlow() {
        assertEquals("取消检测（暂不可用）",
                OperationsDashboardStatusFormatter.flowCancellation(false, false, false, false));
        assertEquals("当前无检测",
                OperationsDashboardStatusFormatter.flowCancellation(true, false, false, false));
        assertEquals("检测运行中（不可取消）",
                OperationsDashboardStatusFormatter.flowCancellation(true, true, false, false));
        assertEquals("取消当前检测",
                OperationsDashboardStatusFormatter.flowCancellation(true, true, true, false));
        assertEquals("正在取消检测…",
                OperationsDashboardStatusFormatter.flowCancellation(true, true, true, true));

        assertFalse(OperationsDashboardStatusFormatter.flowCancellationEnabled(false, true, true, false));
        assertFalse(OperationsDashboardStatusFormatter.flowCancellationEnabled(true, false, true, false));
        assertFalse(OperationsDashboardStatusFormatter.flowCancellationEnabled(true, true, false, false));
        assertFalse(OperationsDashboardStatusFormatter.flowCancellationEnabled(true, true, true, true));
        assertTrue(OperationsDashboardStatusFormatter.flowCancellationEnabled(true, true, true, false));
    }

    private static void assertStatus(String title, String summary, int tone,
            OperationsDashboardStatusFormatter.Item item) {
        assertEquals(title, item.title);
        assertEquals(summary, item.summary);
        assertEquals(tone, item.tone);
    }
}
