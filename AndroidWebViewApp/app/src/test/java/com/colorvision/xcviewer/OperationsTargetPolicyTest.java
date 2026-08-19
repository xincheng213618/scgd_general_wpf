package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsTargetPolicyTest {
    @Test
    public void confirmedActionMustStillBelongToTheSelectedComputer() {
        assertTrue(OperationsTargetPolicy.isSameTarget("host_1", "host_1"));
        assertFalse(OperationsTargetPolicy.isSameTarget("host_1", "host_2"));
        assertFalse(OperationsTargetPolicy.isSameTarget("", ""));
        assertFalse(OperationsTargetPolicy.isSameTarget(null, "host_1"));
    }

    @Test
    public void localComputerLabelIsVisibleInConfirmationsAndNotifications() {
        assertEquals("操作目标：一号线 AOI\n\n确认后重启应用。",
                OperationsTargetPolicy.confirmationMessage(
                        "一号线 AOI", "确认后重启应用。"));
        assertEquals("ColorVision · 一号线 AOI",
                OperationsTargetPolicy.watchNotificationTitle("一号线 AOI", 1));
        assertEquals("ColorVision · 守护 3 台电脑",
                OperationsTargetPolicy.watchNotificationTitle("一号线 AOI", 3));
        assertEquals("一号线 AOI 需要关注",
                OperationsTargetPolicy.attentionNotificationTitle("一号线 AOI"));
    }

    @Test
    public void missingLabelUsesANeutralLocalFallback() {
        assertEquals("ColorVision · 当前电脑",
                OperationsTargetPolicy.watchNotificationTitle("", 1));
        assertEquals("操作目标：当前电脑\n\n继续？",
                OperationsTargetPolicy.confirmationMessage(null, "继续？"));
    }
}
