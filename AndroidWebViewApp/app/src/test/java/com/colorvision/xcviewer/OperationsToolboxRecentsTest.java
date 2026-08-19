package com.colorvision.xcviewer;

import org.junit.Test;

import java.util.List;

import static org.junit.Assert.assertEquals;

public class OperationsToolboxRecentsTest {
    @Test
    public void mostRecentDistinctActionMovesToTheFront() {
        String recents = OperationsToolboxRecents.record(
                "toolbox.snapshot.create\ntoolbox.message.recover",
                "toolbox.message.recover");

        assertEquals(
                "toolbox.message.recover\ntoolbox.snapshot.create",
                recents);
    }

    @Test
    public void onlyFourRecentActionsAreRetained() {
        String recents = "";
        for (int index = 1; index <= 5; index++) {
            recents = OperationsToolboxRecents.record(recents, "toolbox.action." + index);
        }

        List<String> parsed = OperationsToolboxRecents.parse(recents);
        assertEquals(OperationsToolboxRecents.MAX_ITEMS, parsed.size());
        assertEquals("toolbox.action.5", parsed.get(0));
        assertEquals("toolbox.action.2", parsed.get(3));
    }

    @Test
    public void malformedStoredValuesAreIgnoredAndCleanedOnWrite() {
        String recents = OperationsToolboxRecents.record(
                "toolbox.valid\nnot valid\ntoolbox.valid\n",
                "also invalid");

        assertEquals("toolbox.valid", recents);
    }
}
