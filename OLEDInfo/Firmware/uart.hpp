#ifndef UART_HPP
#define UART_HPP

#include <avr/io.h>

namespace UART {
void init(uint16_t baud, bool use_2x = false);
void init(uint8_t ubrrh, uint8_t ubrrl, bool use_2x = false);
void send(uint8_t data, bool async = true);
uint8_t recv(void);
void recv(void (*fx)(uint8_t));
}

#endif