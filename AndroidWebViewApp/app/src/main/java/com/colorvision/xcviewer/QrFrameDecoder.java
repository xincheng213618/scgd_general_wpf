package com.colorvision.xcviewer;

import androidx.annotation.Nullable;

import com.google.zxing.BarcodeFormat;
import com.google.zxing.BinaryBitmap;
import com.google.zxing.DecodeHintType;
import com.google.zxing.MultiFormatReader;
import com.google.zxing.NotFoundException;
import com.google.zxing.PlanarYUVLuminanceSource;
import com.google.zxing.Result;
import com.google.zxing.common.HybridBinarizer;

import java.nio.ByteBuffer;
import java.util.Collections;
import java.util.EnumMap;
import java.util.Map;

final class QrFrameDecoder {
    private final MultiFormatReader reader = createReader();

    @Nullable
    String decode(byte[] luma, int width, int height, int rotationDegrees) {
        if (!isValidFrame(luma, width, height)) {
            return null;
        }
        int rotation = normalizeRotation(rotationDegrees);
        Frame oriented = rotateClockwise(luma, width, height, rotation);
        String result = decodeLuminance(oriented.data, oriented.width, oriented.height);
        if (result == null && rotation != 0) {
            result = decodeLuminance(luma, width, height);
        }
        return result;
    }

    static byte[] copyLumaPlane(
            ByteBuffer buffer,
            int width,
            int height,
            int rowStride,
            int pixelStride) {
        if (buffer == null || width <= 0 || height <= 0 || rowStride <= 0 || pixelStride <= 0) {
            throw new IllegalArgumentException("Invalid luma plane");
        }
        int pixelCount = Math.multiplyExact(width, height);
        ByteBuffer source = buffer.duplicate();
        int start = source.position();
        long lastIndex = (long) start
                + (long) (height - 1) * rowStride
                + (long) (width - 1) * pixelStride;
        if (lastIndex >= source.limit()) {
            throw new IllegalArgumentException("Incomplete luma plane");
        }

        byte[] luma = new byte[pixelCount];
        for (int y = 0; y < height; y++) {
            int sourceRow = start + y * rowStride;
            int targetRow = y * width;
            for (int x = 0; x < width; x++) {
                luma[targetRow + x] = source.get(sourceRow + x * pixelStride);
            }
        }
        return luma;
    }

    private static MultiFormatReader createReader() {
        MultiFormatReader formatReader = new MultiFormatReader();
        Map<DecodeHintType, Object> hints = new EnumMap<>(DecodeHintType.class);
        hints.put(DecodeHintType.POSSIBLE_FORMATS, Collections.singletonList(BarcodeFormat.QR_CODE));
        hints.put(DecodeHintType.CHARACTER_SET, "UTF-8");
        formatReader.setHints(hints);
        return formatReader;
    }

    @Nullable
    private String decodeLuminance(byte[] data, int width, int height) {
        try {
            PlanarYUVLuminanceSource source = new PlanarYUVLuminanceSource(
                    data, width, height, 0, 0, width, height, false);
            Result result = reader.decodeWithState(new BinaryBitmap(new HybridBinarizer(source)));
            return result == null ? null : result.getText();
        } catch (NotFoundException ex) {
            return null;
        } finally {
            reader.reset();
        }
    }

    private static boolean isValidFrame(byte[] data, int width, int height) {
        if (data == null || width <= 0 || height <= 0) {
            return false;
        }
        long required = (long) width * height;
        return required <= Integer.MAX_VALUE && data.length >= required;
    }

    private static int normalizeRotation(int rotationDegrees) {
        int normalized = ((rotationDegrees % 360) + 360) % 360;
        return normalized == 90 || normalized == 180 || normalized == 270 ? normalized : 0;
    }

    private static Frame rotateClockwise(byte[] data, int width, int height, int rotation) {
        if (rotation == 0) {
            return new Frame(data, width, height);
        }
        byte[] rotated = new byte[width * height];
        if (rotation == 180) {
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    rotated[(height - 1 - y) * width + (width - 1 - x)] = data[y * width + x];
                }
            }
            return new Frame(rotated, width, height);
        }
        if (rotation == 90) {
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    rotated[x * height + (height - 1 - y)] = data[y * width + x];
                }
            }
        } else {
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    rotated[(width - 1 - x) * height + y] = data[y * width + x];
                }
            }
        }
        return new Frame(rotated, height, width);
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
