#include "uart.hpp"
#include <avr/interrupt.h>

#define MYUBRR(buad, use_2x) (F_CPU / ((use_2x) ? 8 : 16) / (buad) - 1)

namespace {
    volatile bool sent = true;
    volatile uint8_t buffer;
    void (* volatile recv_fx)(uint8_t);
}

namespace UART {
void init(uint16_t baud, bool use_2x) {
    uint16_t ubrr = MYUBRR(baud, use_2x);
    init((uint8_t) (ubrr >> 8), (uint8_t) ubrr, use_2x);
}

void init(uint8_t ubrrh, uint8_t ubrrl, bool use_2x) {
    UBRRH = ubrrh;
    UBRRL = ubrrl;
    if (use_2x) UCSRA |= _BV(U2X);
    UCSRB |= _BV(RXEN) | _BV(TXEN); // reciver and transmitter mode
    UCSRC |= _BV(URSEL) | _BV(UCSZ1) | _BV(UCSZ0); // 8-bit character size
    UCSRB |= _BV(RXCIE) | _BV(UDRIE); // enable interrupts
}

void send(uint8_t data, bool async) {
    if (!async) {
        loop_until_bit_is_set(UCSRA, UDRE);
        UDR = data;
    } else {
        if (bit_is_set(UCSRA, UDRE)) {
            UDR = data;
        } else {
            buffer = data;
            sent = false;
        }
    }
}

uint8_t recv(void) {
    loop_until_bit_is_set(UCSRA, RXC);
    return UDR;
}

void recv(void (*fx)(uint8_t)) {
    recv_fx = fx;
}
}

ISR (USART_UDRE_vect) {
    if (!sent) {
        UDR = buffer;
        sent = true;
    }
}

ISR (USART_RXC_vect) {
    if (recv_fx != nullptr)
        recv_fx(UDR);
}
