import CoreGraphics
import Foundation
import ImageIO

// usage: pngstats.swift img.png x0 y0 x1 y1 — mean RGB + stdev luminance
let args = CommandLine.arguments
let path = args[1]
let x0 = Int(args[2])!, y0 = Int(args[3])!, x1 = Int(args[4])!, y1 = Int(args[5])!
func load(_ path: String) -> CGImage? {
    guard let src = CGImageSourceCreateWithURL(URL(fileURLWithPath: path) as CFURL, nil) else { return nil }
    return CGImageSourceCreateImageAtIndex(src, 0, nil)
}
guard let img = load(path) else { print("LOAD_FAIL"); exit(2) }
let w = img.width, h = img.height
var data = [UInt8](repeating: 0, count: w*h*4)
let ctx = CGContext(data: &data, width: w, height: h, bitsPerComponent: 8,
                    bytesPerRow: w*4, space: CGColorSpaceCreateDeviceRGB(),
                    bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
ctx.draw(img, in: CGRect(x:0,y:0,width:w,height:h))
guard img.width > 0, img.height > 0 else { print("EMPTY_IMAGE"); exit(4) }
let rx0 = max(0,x0), ry0 = max(0,y0), rx1 = min(w,x1), ry1 = min(h,y1)
guard rx0 < rx1, ry0 < ry1 else { print("EMPTY_REGION"); exit(4) }
var n = 0, sr = 0.0, sg = 0.0, sb = 0.0, sl = 0.0, sl2 = 0.0
for y in ry0..<ry1 { for x in rx0..<rx1 {
    let o = (y*w+x)*4
    let r = Double(data[o]), g = Double(data[o+1]), b = Double(data[o+2])
    let l = 0.299*r + 0.587*g + 0.114*b
    sr += r; sg += g; sb += b; sl += l; sl2 += l*l; n += 1
}}
let mean = Double(n)
let lumMean = sl/mean
let rawLumVar = sl2/mean - lumMean*lumMean
let lumVar = rawLumVar < 0 ? 0 : rawLumVar
print(String(format: "meanRGB=(%.1f,%.1f,%.1f) lumMean=%.1f lumSD=%.1f n=%d", sr/mean, sg/mean, sb/mean, lumMean, lumVar.squareRoot(), n))
