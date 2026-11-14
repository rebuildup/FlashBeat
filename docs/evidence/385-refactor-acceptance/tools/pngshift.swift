import CoreGraphics
import Foundation
import ImageIO
// usage: pngshift.swift a.png b.png x0 y0 x1 y1 — try shifts, report best
let args = CommandLine.arguments
func load(_ p: String) -> CGImage? {
    guard let s = CGImageSourceCreateWithURL(URL(fileURLWithPath: p) as CFURL, nil) else { return nil }
    return CGImageSourceCreateImageAtIndex(s, 0, nil)
}
let a = load(args[1])!, b = load(args[2])!
let x0 = Int(args[3])!, y0 = Int(args[4])!, x1 = Int(args[5])!, y1 = Int(args[6])!
let w = a.width, h = a.height
func bytes(_ img: CGImage) -> [UInt8] {
    var d = [UInt8](repeating: 0, count: w*h*4)
    let c = CGContext(data: &d, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w*4, space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    c.draw(img, in: CGRect(x:0,y:0,width:w,height:h)); return d
}
let ba = bytes(a), bb = bytes(b)
func diffAt(_ dx: Int, _ dy: Int) -> Int {
    var d = 0
    for y in max(0,y0)..<min(h,y1) { for x in max(0,x0)..<min(w,x1) {
        let ox = x+dx, oy = y+dy
        if ox < 0 || oy < 0 || ox >= w || oy >= h { continue }
        let o1 = (y*w+x)*4, o2 = (oy*w+ox)*4
        let dd = abs(Int(ba[o1])-Int(bb[o2])) + abs(Int(ba[o1+1])-Int(bb[o2+1])) + abs(Int(ba[o1+2])-Int(bb[o2+2]))
        if dd > 90 { d += 1 }
    }}
    return d
}
for dy in -6...6 { var line = "dy=\(dy): "; for dx in -6...6 { line += String(format: "%6d ", diffAt(dx,dy)) }; print(line) }
