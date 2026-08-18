package com.colorvision.xcviewer;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class OperationsRemoteActionPresentationTest {
    @Test
    public void staleRelayNoteExplainsQueueAndResultConfirmation() {
        String note = OperationsRemoteActionPresentation.scopeNote(true, false);

        assertTrue(note.contains("电脑当前离线"));
        assertTrue(note.contains("固定中继短时等待"));
        assertTrue(note.contains("最近远程请求"));
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
