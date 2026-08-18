package com.colorvision.xcviewer;

import static org.junit.Assert.assertArrayEquals;
import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNull;

import com.google.zxing.BarcodeFormat;
import com.google.zxing.WriterException;
import com.google.zxing.common.BitMatrix;
import com.google.zxing.qrcode.QRCodeWriter;

import org.junit.Test;

import java.nio.ByteBuffer;

public class QrFrameDecoderTest {
    private static final String PAYLOAD = "colorvision://pair?payload=test-camera-x-orientation";

    @Test
    public void decodesQrCodeAcrossCameraRotations() throws Exception {
        Frame upright = qrFrame(PAYLOAD);
        QrFrameDecoder decoder = new QrFrameDecoder();

        assertEquals(PAYLOAD, decoder.decode(upright.data, upright.width, upright.height, 0));
        Frame sensor90 = rotateCounterClockwise(upright);
        assertEquals(PAYLOAD, decoder.decode(sensor90.data, sensor90.width, sensor90.height, 90));
        Frame sensor180 = rotateCounterClockwise(rotateCounterClockwise(upright));
        assertEquals(PAYLOAD, decoder.decode(sensor180.data, sensor180.width, sensor180.height, 180));
        Frame sensor270 = rotateCounterClockwise(sensor180);
        assertEquals(PAYLOAD, decoder.decode(sensor270.data, sensor270.width, sensor270.height, 270));
    }

    @Test
    public void copiesLumaPlaneWithRowPaddingAndPixelStride() {
        ByteBuffer source = ByteBuffer.allocate(16);
        source.put(0, (byte) 1);
        source.put(2, (byte) 2);
        source.put(4, (byte) 3);
        source.put(8, (byte) 4);
        source.put(10, (byte) 5);
        source.put(12, (byte) 6);

        assertArrayEquals(new byte[]{1, 2, 3, 4, 5, 6},
                QrFrameDecoder.copyLumaPlane(source, 3, 2, 8, 2));
    }

    @Test
    public void rejectsIncompleteOrInvalidFrames() {
        QrFrameDecoder decoder = new QrFrameDecoder();

        assertNull(decoder.decode(null, 10, 10, 0));
        assertNull(decoder.decode(new byte[4], 3, 3, 0));
    }

    private static Frame qrFrame(String text) throws WriterException {
        BitMatrix matrix = new QRCodeWriter().encode(text, BarcodeFormat.QR_CODE, 192, 192);
        byte[] data = new byte[matrix.getWidth() * matrix.getHeight()];
        for (int y = 0; y < matrix.getHeight(); y++) {
            for (int x = 0; x < matrix.getWidth(); x++) {
                data[y * matrix.getWidth() + x] = matrix.get(x, y) ? 0 : (byte) 0xff;
            }
        }
        return new Frame(data, matrix.getWidth(), matrix.getHeight());
    }

    private static Frame rotateCounterClockwise(Frame source) {
        byte[] rotated = new byte[source.width * source.height];
        for (int y = 0; y < source.height; y++) {
            for (int x = 0; x < source.width; x++) {
                int newX = y;
                int newY = source.width - 1 - x;
                rotated[newY * source.height + newX] = source.data[y * source.width + x];
            }
        }
        return new Frame(rotated, source.height, source.width);
    }

    private static final class Frame {
        final byte[] data;
        final int width;
        final int height;

        Frame(byte[] data, int width, int height) {
            this.data = data;
            this.width = width;
            this.height = height;
        }
    }
}
