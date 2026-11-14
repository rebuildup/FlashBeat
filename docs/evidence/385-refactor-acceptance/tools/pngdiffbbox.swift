import CoreGraphics
import Foundation
import ImageIO

let args = CommandLine.arguments
let aPath = args[1], bPath = args[2]
let thresh = args.count > 3 ? Int(args[3])! : 30

func load(_ path: String) -> CGImage? {
    guard let src = CGImageSourceCreateWithURL(URL(fileURLWithPath: path) as CFURL, nil) else { return nil }
    return CGImageSourceCreateImageAtIndex(src, 0, nil)
}
guard let a = load(aPath), let b = load(bPath) else { print("LOAD_FAIL"); exit(2) }
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
var minX = w, minY = h, maxX = -1, maxY = -1, diff = 0
var rowDiff = [Int](repeating: 0, count: h)
var colDiff = [Int](repeating: 0, count: w)
for y in 0..<h { for x in 0..<w {
    let o = (y*w+x)*4
    let d = abs(Int(ba[o])-Int(bb[o])) + abs(Int(ba[o+1])-Int(bb[o+1])) + abs(Int(ba[o+2])-Int(bb[o+2]))
    if d > thresh {
        diff += 1; rowDiff[y] += 1; colDiff[x] += 1
        if x < minX { minX = x }; if x > maxX { maxX = x }
        if y < minY { minY = y }; if y > maxY { maxY = y }
    }
}}
print(String(format: "diff=%d bbox=(%d,%d)-(%d,%d)", diff, minX, minY, maxX, maxY))
// rows with >0.5% of width diffing, grouped into bands
var inBand = false; var start = 0
for y in 0...h {
    let active = y < h && rowDiff[y] > w/200
    if (active && !inBand) { start = y; inBand = true }
    if (!active && inBand) { print(String(format: "rowband %d-%d (%.1f%% of height)", start, y-1, 100.0*Double(y-start)/Double(h))); inBand = false }
}
// top diff columns summary
var bands: [(Int,Int)] = []
inBand = false
for x in 0...w {
    let active = x < w && colDiff[x] > h/200
    if (active && !inBand) { start = x; inBand = true }
    if (!active && inBand) { bands.append((start, x-1)); inBand = false }
}
print("colbands:", bands.map { "\($0.0)-\($0.1)" }.joined(separator: " "))
