package com.colorvision.xcviewer;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class OperationsRemoteActionPresentationTest {
    @Test
    public void staleRelayNoteSeparatesPausedControlsFromQueuedDiagnostics() {
        String note = OperationsRemoteActionPresentation.scopeNote(true, false);

        assertTrue(note.contains("电脑当前离线"));
        assertTrue(note.contains("窗口控制已暂停"));
        assertTrue(note.contains("15 分钟"));
        assertTrue(note.contains("最近远程请求"));
    }

    @Test
    public void staleRelayDescriptionsDoNotPresentHistoricalControlsAsCurrent() {
        assertTrue(OperationsRemoteActionPresentation.windowDescription(true, false)
                .contains("上线后自动恢复"));
        assertTrue(OperationsRemoteActionPresentation.diagnosticsDescription(true, false)
                .contains("上次安全快照"));
    }

    @Test
    public void freshRelayNoteExplainsSignedExecution() {
        String note = OperationsRemoteActionPresentation.scopeNote(true, true);

        assertTrue(note.contains("设备密钥签名"));
        assertTrue(note.contains("签名结果"));
        assertFalse(note.contains("离线"));
    }

    @Test
    public void localNoteKeepsDisruptiveActionsBehindConfirmation() {
        String note = OperationsRemoteActionPresentation.scopeNote(false, true);

        assertTrue(note.contains("当前电脑"));
        assertTrue(note.contains("再次确认"));
    }
}
