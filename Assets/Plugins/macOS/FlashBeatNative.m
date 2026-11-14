#import <Cocoa/Cocoa.h>
#import <objc/runtime.h>
#import <objc/message.h>

static void FBLog(NSString *msg)
{
    NSString *line = [@"[FlashBeatNative] " stringByAppendingString:msg];
    NSLog(@"%@", line);
    FILE *f = fopen("/tmp/FBNative.log", "a");
    if (f) { fprintf(f, "%s\n", [line UTF8String]); fclose(f); }
}

static NSWindow *FBFindWindow(void)
{
    for (NSWindow *w in [NSApp windows]) {
        if ([NSStringFromClass([w class]) containsString:@"PlayerWindow"]) return w;
    }
    return nil;
}

static void FBSetCommon(NSWindow *win, NSRect r)
{
    SEL c = NSSelectorFromString(@"_setFrameCommon:display:fromServer:");
    if ([win respondsToSelector:c]) {
        ((void (*)(id, SEL, NSRect, BOOL, BOOL))objc_msgSend)(win, c, r, YES, YES);
    }
}

static NSTimer *g_timer = nil;
static __weak NSWindow *g_win = nil;

static void FBEnforce(void)
{
    @try {
        NSWindow *win = g_win;
        if (!win) { win = FBFindWindow(); g_win = win; }
        if (!win) return;
        NSScreen *s = [win screen] ?: [NSScreen mainScreen];
        if (!s) return;
        NSRect full = s.frame;
        // Unity が visibleFrame に戻そうとするのを継続的に矯正する
        if (!NSEqualRects(win.frame, full)) {
            FBSetCommon(win, full);
        }
    } @catch (NSException *e) { }
}

static void FBEnterWhenReady(int attemptsLeft)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        @try {
            [NSApp setPresentationOptions:NSApplicationPresentationAutoHideMenuBar
                | NSApplicationPresentationAutoHideDock];
            NSWindow *win = FBFindWindow();
            if (!win) {
                if (attemptsLeft > 0) {
                    FBLog(@"window not ready, retrying");
                    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.5 * NSEC_PER_SEC)),
                        dispatch_get_main_queue(), ^{ FBEnterWhenReady(attemptsLeft - 1); });
                } else {
                    FBLog(@"no window after retries");
                }
                return;
            }
            g_win = win;
            FBLog([NSString stringWithFormat:@"before=%@", NSStringFromRect(win.frame)]);
            [win toggleFullScreen:nil];
            FBLog(@"toggle called");
            FBSetCommon(win, ([win screen] ?: [NSScreen mainScreen]).frame);
            if (!g_timer) {
                g_timer = [NSTimer timerWithTimeInterval:0.2 repeats:YES block:^(NSTimer *t){ FBEnforce(); }];
                [[NSRunLoop mainRunLoop] addTimer:g_timer forMode:NSRunLoopCommonModes];
            }
            FBLog(@"enforcer started");
        } @catch (NSException *e) {
            FBLog([@"exception: " stringByAppendingString:e.reason]);
        }
    });
}

void FBEnterNativeFullscreen(void)
{
    FBLog(@"enter called");
    FBEnterWhenReady(40);
}
