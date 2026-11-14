import CoreGraphics
import Foundation
import ImageIO

// usage: pngdiffascii.swift a.png b.png cols rows — ASCII heatmap of diff density
let args = CommandLine.arguments
let aPath = args[1], bPath = args[2]
let cols = Int(args[3])!, rows = Int(args[4])!
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
let ramp: [Character] = [" ", ".", ":", "o", "O", "#", "@"]
for r in 0..<rows {
    var line = ""
    for c in 0..<cols {
        let x0 = c*w/cols, x1 = (c+1)*w/cols, y0 = r*h/rows, y1 = (r+1)*h/rows
        var diff = 0, tot = 0
        for y in y0..<y1 { for x in x0..<x1 {
            let o = (y*w+x)*4
            let d = abs(Int(ba[o])-Int(bb[o])) + abs(Int(ba[o+1])-Int(bb[o+1])) + abs(Int(ba[o+2])-Int(bb[o+2]))
            if d > 60 { diff += 1 }; tot += 1
        }}
        let p = 100.0*Double(diff)/Double(tot)
        let idx = min(ramp.count-1, max(0, Int(p/4.0)))
        line.append(ramp[idx])
    }
    print(line)
}
