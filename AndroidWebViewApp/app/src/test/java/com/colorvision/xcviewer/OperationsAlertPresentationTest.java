package com.colorvision.xcviewer;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class OperationsAlertPresentationTest {
    @Test
    public void detailFallbackChoosesTheHighestSeveritySafeSource() throws Exception {
        JSONObject summary = new JSONObject()
                .put("warningCount", 2)
                .put("errorCount", 1)
                .put("criticalCount", 0);
        JSONObject response = new JSONObject("{\"data\":{\"alerts\":["
                + "{\"severity\":\"warning\",\"source\":\"安全运维\"},"
                + "{\"severity\":\"error\",\"source\":\"消息服务\"}"
                + "]}}");

        assertEquals("消息服务",
                OperationsAlertPresentation.primarySourceFromDetails(summary, response));
    }

    @Test
    public void detailFallbackRejectsArbitrarySourceNames() throws Exception {
        JSONObject summary = new JSONObject().put("warningCount", 1);
        JSONObject response = new JSONObject("{\"alerts\":["
                + "{\"severity\":\"warning\",\"source\":\"private-plugin-name\"}"
                + "]}");

        assertEquals("",
                OperationsAlertPresentation.primarySourceFromDetails(summary, response));
        assertEquals("", OperationsAlertPresentation.safeSource("private-plugin-name"));
    }
}
