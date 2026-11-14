import CoreGraphics
import Foundation
import ImageIO

// usage: swift pngdiff.swift a.png b.png [threshold]
// prints per-pixel diff stats; exit 1 if diffCount > 0 beyond threshold
let args = CommandLine.arguments
let aPath = args[1], bPath = args[2]
let threshold = args.count > 3 ? Int(args[3])! : 0

func load(_ path: String) -> CGImage? {
    guard let src = CGImageSourceCreateWithURL(URL(fileURLWithPath: path) as CFURL, nil),
          let img = CGImageSourceCreateImageAtIndex(src, 0, nil) else { return nil }
    return img
}
guard let a = load(aPath), let b = load(bPath) else { print("LOAD_FAIL"); exit(2) }
guard a.width == b.width, a.height == b.height else {
    print("SIZE_MISMATCH \(a.width)x\(a.height) vs \(b.width)x\(b.height)"); exit(3)
}
let w = a.width, h = a.height
func bytes(_ img: CGImage) -> [UInt8] {
    let cs = CGColorSpaceCreateDeviceRGB()
    var data = [UInt8](repeating: 0, count: w*h*4)
    let ctx = CGContext(data: &data, width: w, height: h, bitsPerComponent: 8,
                        bytesPerRow: w*4, space: cs,
                        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    ctx.draw(img, in: CGRect(x:0,y:0,width:w,height:h))
    return data
}
let ba = bytes(a), bb = bytes(b)
var diff = 0, maxd = 0
var sum = 0
for i in stride(from: 0, to: ba.count, by: 4) {
    let d = abs(Int(ba[i])-Int(bb[i])) + abs(Int(ba[i+1])-Int(bb[i+1])) + abs(Int(ba[i+2])-Int(bb[i+2]))
    if d > threshold { diff += 1 }
    if d > maxd { maxd = d }
    sum += d
}
let total = w*h
print(String(format: "diffPixels=%d/%d (%.4f%%) maxChannelSum=%d mean=%.4f", diff, total, 100.0*Double(diff)/Double(total), maxd, Double(sum)/Double(total)/3.0))
exit(diff > 0 ? 1 : 0)
