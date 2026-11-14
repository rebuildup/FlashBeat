import CoreGraphics
import Foundation
import ImageIO
// usage: pngcolprof.swift img.png y0 y1 xstep — per-column-band luminance SD profile
let args = CommandLine.arguments
let path = args[1]; let y0 = Int(args[2])!, y1 = Int(args[3])!, step = Int(args[4])!
guard step > 0 else { print("BAD_STEP"); exit(4) }
func load(_ p: String) -> CGImage? {
    guard let s = CGImageSourceCreateWithURL(URL(fileURLWithPath: p) as CFURL, nil) else { return nil }
    return CGImageSourceCreateImageAtIndex(s, 0, nil)
}
guard let img = load(path) else { print("LOAD_FAIL"); exit(2) }
let w = img.width, h = img.height
var data = [UInt8](repeating: 0, count: w*h*4)
let ctx = CGContext(data: &data, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w*4, space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
ctx.draw(img, in: CGRect(x:0,y:0,width:w,height:h))
var x = 0
while x < w {
    var n = 0, sl = 0.0, sl2 = 0.0
    for y in max(0,y0)..<min(h,y1) { for xx in x..<min(w,x+step) {
        let o = (y*w+xx)*4
        let l = 0.299*Double(data[o]) + 0.587*Double(data[o+1]) + 0.114*Double(data[o+2])
        sl += l; sl2 += l*l; n += 1
    }}
    let m = sl/Double(n)
    let rawVar = sl2/Double(n) - m*m
    let sd = (rawVar < 0 ? 0 : rawVar).squareRoot()
    print(String(format: "%5d-%5d mean=%6.1f sd=%6.1f", x, min(w,x+step)-1, m, sd))
    x += step
}
