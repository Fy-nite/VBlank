; data_test.asm
; Test multi-byte directives and forward label fixups

    ; Print message
    mov rax, 1
    mov rdi, 1
    mov rsi, hello_msg
    mov rdx, 14
    syscall

    ; Exit program
    mov rax, 60
    mov rdi, 0
    syscall
section .text
hello_msg:
    db "Hello form asm"

; Data definitions
    
val64:  dq 0x1122334455667788
val32:  dd 0xAABBCCDD
val16:  dw 0x1234
val8:   db 0x55

    ; Relocation tests
ptr_to_val8:    dq val8        ; Backward ref (resolved immediately)
ptr_to_end:     dq end_marker  ; Forward ref (needs fixup)
ptr_to_end_32:  dd end_marker  ; Forward ref 32-bit (needs fixup)
ptr_to_end_16:  dw end_marker  ; Forward ref 16-bit (needs fixup)

end_marker:
    db 0xEE
