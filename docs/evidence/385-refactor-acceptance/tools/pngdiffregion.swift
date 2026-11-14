import CoreGraphics
import Foundation
import ImageIO

// usage: pngdiffregion.swift a.png b.png x0 y0 x1 y1 [threshold]
let args = CommandLine.arguments
let aPath = args[1], bPath = args[2]
let x0 = Int(args[3])!, y0 = Int(args[4])!, x1 = Int(args[5])!, y1 = Int(args[6])!
let thresh = args.count > 7 ? Int(args[7])! : 30
func load(_ path: String) -> CGImage? {
    guard let src = CGImageSourceCreateWithURL(URL(fileURLWithPath: path) as CFURL, nil) else { return nil }
    return CGImageSourceCreateImageAtIndex(src, 0, nil)
}
guard let a = load(aPath), let b = load(bPath) else { print("LOAD_FAIL"); exit(2) }
guard a.width == b.width, a.height == b.height else {
    print("SIZE_MISMATCH \(a.width)x\(a.height) vs \(b.width)x\(b.height)"); exit(3)
}
let w = a.width, h = a.height
func bytes(_ img: CGImage) -> [UInt8] {
    var data = [UInt8](repeating: 0, count: w*h*4)
    let ctx = CGContext(data: &data, width: w, height: h, bitsPerComponent: 8,
                        bytesPerRow: w*4, space: CGColorSpaceCreateDeviceRGB(),
                        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    ctx.draw(img, in: CGRect(x:0,y:0,width:w,height:h))
    return data
}
let ba = bytes(a), bb = bytes(b)
let rx0 = max(0, x0), ry0 = max(0, y0), rx1 = min(w, x1), ry1 = min(h, y1)
guard rx0 < rx1, ry0 < ry1 else { print("EMPTY_REGION"); exit(4) }
var diff = 0, tot = 0, sum = 0
for y in ry0..<ry1 { for x in rx0..<rx1 {
    let o = (y*w+x)*4
    let d = abs(Int(ba[o])-Int(bb[o])) + abs(Int(ba[o+1])-Int(bb[o+1])) + abs(Int(ba[o+2])-Int(bb[o+2]))
    if d > thresh { diff += 1 }; sum += d; tot += 1
}}
print(String(format: "region(%d,%d,%d,%d) thresh=%d: diff=%d/%d (%.2f%%) mean=%.3f", x0,y0,x1,y1, thresh, diff, tot, 100.0*Double(diff)/Double(tot), Double(sum)/Double(tot)/3.0))
