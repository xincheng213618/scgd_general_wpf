package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

public class PairingSuccessPresentationTest {
    @Test
    public void newPairingNamesTheCurrentComputerAndOffersRename() {
        String message = PairingSuccessPresentation.message(false, "电脑 2");

        assertTrue(message.contains("已安全配对"));
        assertTrue(message.contains("当前电脑：电脑 2"));
        assertEquals("命名电脑", PairingSuccessPresentation.renameAction());
    }

    @Test
    public void repairedAndUnnamedProfilesRemainClear() {
        assertTrue(PairingSuccessPresentation.message(true, "生产线").contains("已更新安全配对"));
        assertTrue(PairingSuccessPresentation.message(false, " ").contains("当前电脑：这台电脑"));
    }
}
