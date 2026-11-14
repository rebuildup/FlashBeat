import CoreGraphics
import Foundation
import ImageIO
// usage: pngshift.swift a.png b.png x0 y0 x1 y1 — try shifts, report best
// All shift candidates are evaluated over the SAME pixel set: the region
// eroded by the maximum shift magnitude (6px) on every side, so every
// candidate compares a[y,x] in a against b[y+dy,x+dx] for the same (x,y).
// If that common region is empty the tool reports EMPTY_COMMON_REGION
// (exit 4) instead of returning 0, which would look like a perfect match.
let args = CommandLine.arguments
func load(_ p: String) -> CGImage? {
    guard let s = CGImageSourceCreateWithURL(URL(fileURLWithPath: p) as CFURL, nil) else { return nil }
    return CGImageSourceCreateImageAtIndex(s, 0, nil)
}
guard let a = load(args[1]), let b = load(args[2]) else { print("LOAD_FAIL"); exit(2) }
guard a.width == b.width, a.height == b.height else {
    print("SIZE_MISMATCH \(a.width)x\(a.height) vs \(b.width)x\(b.height)"); exit(3)
}
let x0 = Int(args[3])!, y0 = Int(args[4])!, x1 = Int(args[5])!, y1 = Int(args[6])!
let w = a.width, h = a.height
let maxShift = 6
// common domain: every (x,y) in the requested region whose shifted
// position stays in bounds for ALL candidates dx,dy in [-maxShift,maxShift]
let cx0 = max(x0, maxShift), cy0 = max(y0, maxShift)
let cx1 = min(x1, w - maxShift), cy1 = min(y1, h - maxShift)
guard cx0 < cx1, cy0 < cy1 else { print("EMPTY_COMMON_REGION"); exit(4) }
func bytes(_ img: CGImage) -> [UInt8] {
    var d = [UInt8](repeating: 0, count: w*h*4)
    let c = CGContext(data: &d, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w*4, space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    c.draw(img, in: CGRect(x:0,y:0,width:w,height:h)); return d
}
let ba = bytes(a), bb = bytes(b)
func diffAt(_ dx: Int, _ dy: Int) -> Int {
    var d = 0
    for y in cy0..<cy1 { for x in cx0..<cx1 {
        let ox = x+dx, oy = y+dy
        let o1 = (y*w+x)*4, o2 = (oy*w+ox)*4
        let dd = abs(Int(ba[o1])-Int(bb[o2])) + abs(Int(ba[o1+1])-Int(bb[o2+1])) + abs(Int(ba[o1+2])-Int(bb[o2+2]))
        if dd > 90 { d += 1 }
    }}
    return d
}
print("commonRegion=(\(cx0),\(cy0))-(\(cx1),\(cy1)) n=\((cx1-cx0)*(cy1-cy0))")
for dy in -maxShift...maxShift { var line = "dy=\(dy): "; for dx in -maxShift...maxShift { line += String(format: "%6d ", diffAt(dx,dy)) }; print(line) }
